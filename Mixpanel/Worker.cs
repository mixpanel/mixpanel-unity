using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using mixpanel.queue;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Web;

#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace mixpanel
{
    public static partial class Worker
    {
        private enum ThreadOperation
        {
            UNDEFINED,
            ENQUEUE_EVENTS,
            ENQUEUE_PEOPLE,
            KILL_THREAD,
            FLUSH,
            RETRY_FLUSH,
        }
        private static Queue<ThreadOperation> _ops = new Queue<ThreadOperation>();
        private static readonly HttpClient _client = new HttpClient();
        private static Thread _bgThread;

        private static bool _stopThread = false;
        private static bool _isBgThreadRunning = false;
        private static int _retryCount = 0;
        private static System.Threading.Timer _retryTimer = null;

        #region ThreadLifecycle

        internal static void StartWorkerThread()
        {
            if (_bgThread == null)
            {
                _bgThread = new Thread(RunBackgroundThread);
                _bgThread.Start();
            }
        }

        private static void StopWorkerThread()
        {
            _stopThread = true;
            if (_retryTimer != null)
            {
                _retryTimer.Dispose();
            }
            ForceStop();
        }

        private static void ForceStop()
        {
            _ops.Enqueue(ThreadOperation.KILL_THREAD);
        }

        #endregion

        #region Operations

        internal static void EnqueueEventOp()
        {
            _ops.Enqueue(ThreadOperation.ENQUEUE_EVENTS);
            if (!_isBgThreadRunning)
            {
                DispatchOperations();
            }
        }

        internal static void EnqueuePeopleOp()
        {
            _ops.Enqueue(ThreadOperation.ENQUEUE_PEOPLE);
            if (!_isBgThreadRunning)
            {
                DispatchOperations();
            }
        }

        internal static void FlushOp()
        {
            if (_retryCount > 0) return;
            ForceFlushOp();
        }

        private static void ForceFlushOp()
        {
            _ops.Enqueue(ThreadOperation.FLUSH);
            if (!_isBgThreadRunning)
            {
                DispatchOperations();
            }
        }

        #endregion

        private static void RunBackgroundThread()
        {
            _isBgThreadRunning = true;
            while (!_stopThread)
            {
                try
                {
                    DispatchOperations();
                }
                catch (Exception e)
                {
                    Mixpanel.LogError(e.ToString());
                }  
            }
            _isBgThreadRunning = false;
        }

        private static void DispatchOperations()
        {
            if (_ops.Count == 0) return;
            ThreadOperation operation = _ops.Dequeue();
            switch (operation)
            {
                case ThreadOperation.ENQUEUE_EVENTS:
                    EnqueueMixpanelQueue(MixpanelStorage.TrackPersistentQueue, Controller.GetInstance().TrackQueue);
                    break;
                case ThreadOperation.ENQUEUE_PEOPLE:
                    EnqueueMixpanelQueue(MixpanelStorage.EngagePersistentQueue, Controller.GetInstance().EngageQueue);
                    break;
                case ThreadOperation.FLUSH:
                    if (_isBgThreadRunning)
                    {
                        IEnumerator trackEnum = SendData(MixpanelStorage.TrackPersistentQueue, Config.TrackUrl);
                        IEnumerator engageEnum = SendData(MixpanelStorage.EngagePersistentQueue, Config.EngageUrl);
                        while (trackEnum.MoveNext()) {};
                        while (engageEnum.MoveNext()) {};
                    }
                    else
                    {
                        Controller.GetInstance().StartCoroutine(SendData(MixpanelStorage.TrackPersistentQueue, Config.TrackUrl));
                        Controller.GetInstance().StartCoroutine(SendData(MixpanelStorage.EngagePersistentQueue, Config.EngageUrl));
                    }
                    break;
                case ThreadOperation.KILL_THREAD:
                    _isBgThreadRunning = false;
                    _bgThread.Abort(); // Will throw an exception
                    break;
                default:
                    break;
            }
        }

        internal static IEnumerator SendData(PersistentQueue persistentQueue, string url)
        {
            if (persistentQueue.CurrentCountOfItemsInQueue == 0) yield break;
            
            int depth = persistentQueue.CurrentCountOfItemsInQueue;
            int numBatches = (depth / Config.BatchSize) + (depth % Config.BatchSize != 0 ? 1 : 0);
            for (int i = 0; i < numBatches; i++)
            {
                if (_stopThread) yield break;
                Value batch = Mixpanel.ArrayPool.Get();
                using (PersistentQueueSession session = persistentQueue.OpenSession())
                {
                    int count = 0;
                    while (count < Config.BatchSize)
                    {
                        byte[] data = session.Dequeue();
                        if (data == null) break;
                        batch.Add(JsonUtility.FromJson<Value>(Encoding.UTF8.GetString(data)));
                        ++count;
                    }
                    if (count == 0) yield break;
                    string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(batch.ToString()));
                    Mixpanel.Log($"Sending Request - '{url}' with payload '{payload}'");
                    bool successful = false;
                    int responseCode = -1;
                    string response = null;

                    if (_isBgThreadRunning)
                    {
                        try
                        {
                            var content = new StringContent("data=" + payload, Encoding.UTF8, "application/json");
                            var responseRequest = _client.PostAsync(url, content).Result;
                            responseCode = (int) responseRequest.StatusCode;
                            response = responseRequest.Content.ToString();
                        }
                        catch (Exception e)
                        {
                            Mixpanel.LogError("There was an error sending the request: " + e);
                        }
                    }
                    else
                    {
                        WWWForm form = new WWWForm();
                        form.AddField("data", payload);
                        UnityWebRequest request = UnityWebRequest.Post(url, form);
                        yield return request.SendWebRequest();
                        while (!request.isDone) yield return new WaitForEndOfFrame();
                        responseCode = (int) request.responseCode;
                        response = request.downloadHandler.text;
                    }

                    Mixpanel.Log($"Response - '{url}' was '{response}'");

                    successful = responseCode == (int) HttpStatusCode.OK;

                    if (successful)
                    {
                        _retryCount = 0;
                        session.Flush();
                        Mixpanel.Put(batch);
                    }
                    else
                    {
                        _retryCount += 1;
                        double retryIn = Math.Pow(2, _retryCount) * 60000;
                        retryIn = Math.Min(retryIn, 10 * 60 * 1000); // limit 10 min
                        Mixpanel.Log("Retrying request in " + retryIn / 1000 + " seconds (retryCount=" + _retryCount + ")");
                        _retryTimer = new System.Threading.Timer((obj) =>
                        {
                            ForceFlushOp();
                            _retryTimer.Dispose();
                        }, null, (int) retryIn, System.Threading.Timeout.Infinite);
                        yield break;
                    }
                }
            }
        }

        internal static void EnqueueMixpanelQueue(PersistentQueue persistentQueue, List<Value> queue)
        {
            lock (queue)
            {
                using (PersistentQueueSession session = persistentQueue.OpenSession())
                {
                    foreach (Value item in queue)
                    {
                        session.Enqueue(Encoding.UTF8.GetBytes(JsonUtility.ToJson(item)));
                        Mixpanel.Put(item);
                    }
                    session.Flush();
                }
                queue.Clear();
            }
        }
    }

    internal static class Util
    {
        internal static double CurrentTime()
        {
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            double currentEpochTime = (DateTime.UtcNow - epochStart).TotalSeconds;
            return currentEpochTime;
        }

        internal static string CurrentDateTime()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        }

        internal static string GetRadio()
        {
            switch(Application.internetReachability)
            {
                case NetworkReachability.NotReachable :
                    return "none";
                case NetworkReachability.ReachableViaCarrierDataNetwork :
                    return "carrier";
                case NetworkReachability.ReachableViaLocalAreaNetwork :
                    return "wifi";
            }
            return "none";
        }
    }
}
