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

namespace mixpanel
{
    public static partial class Worker
    {
        internal class ThreadOperation
        {
            internal enum ThreadOperationAction
            {
                UNDEFINED,
                ENQUEUE_EVENTS,
                ENQUEUE_PEOPLE,
                KILL_THREAD,
                FLUSH,
                RETRY_FLUSH,
                CLEAR_QUEUE,
            }

            ThreadOperationAction action;
            Value what;

            internal ThreadOperation(ThreadOperationAction action)
            {
                this.action = action;
            }

            internal ThreadOperation(ThreadOperationAction action, Value what)
            {
                this.action = action;
                this.what = what;
            }

            internal ThreadOperationAction GetAction()
            {
                return this.action;
            }

            internal Value GetWhat()
            {
                return this.what;
            }
        }

        internal class BlockingQueue<T> where T : class
        {
            readonly int _Size = 0;
            readonly Queue<T> _Queue = new Queue<T>();
            readonly object _Key = new object();
            bool _Quit = false;

            public BlockingQueue(int size)
            {
                _Size = size;
            }

            public void Quit()
            {
                lock (_Key)
                {
                    _Quit = true;
                    Monitor.PulseAll(_Key);
                }
            }

            public void Start()
            {
                lock (_Key)
                {
                    _Quit = false;
                    Monitor.PulseAll(_Key);
                }
            }

            public bool Enqueue(T t)
            {
                lock (_Key)
                {
                    while (!_Quit && _Queue.Count >= _Size) Monitor.Wait(_Key);

                    if (_Quit) return false;

                    _Queue.Enqueue(t);

                    Monitor.PulseAll(_Key);
                }

                return true;
            }

            public T Dequeue()
            {
                T t;
                lock (_Key)
                {
                    while (!_Quit && _Queue.Count == 0) Monitor.Wait(_Key);

                    if (_Queue.Count == 0) return null;

                    t = _Queue.Dequeue();

                    Monitor.PulseAll(_Key);
                }

                return t;
            }
        }

        private static BlockingQueue<ThreadOperation> _ops = new BlockingQueue<ThreadOperation>(16);
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
                _stopThread = false;
                _ops.Start();
                _bgThread = new Thread(RunBackgroundThread);
                _bgThread.Start();
            }
        }

        internal static void StopWorkerThread()
        {
            _stopThread = true;
            _ops.Quit();
            if (_retryTimer != null)
            {
                _retryTimer.Dispose();
            }
        }

        private static void ForceStop()
        {
            _ops.Enqueue(new ThreadOperation(ThreadOperation.ThreadOperationAction.KILL_THREAD));
        }

        #endregion

        #region Operations

        internal static void EnqueueEventOp(Value data)
        {
            _ops.Enqueue(new ThreadOperation(ThreadOperation.ThreadOperationAction.ENQUEUE_EVENTS, data));
            if (!_isBgThreadRunning)
            {
                DispatchOperations();
            }
        }

        internal static void EnqueuePeopleOp(Value data)
        {
            _ops.Enqueue(new ThreadOperation(ThreadOperation.ThreadOperationAction.ENQUEUE_PEOPLE, data));
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
            _ops.Enqueue(new ThreadOperation(ThreadOperation.ThreadOperationAction.FLUSH));
            if (!_isBgThreadRunning)
            {
                DispatchOperations();
            }
        }

        internal static void ClearOp()
        {
            _ops.Enqueue(new ThreadOperation(ThreadOperation.ThreadOperationAction.CLEAR_QUEUE));
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
            _bgThread = null;
            _isBgThreadRunning = false;
        }

        private static void DispatchOperations()
        {
            ThreadOperation operation = _ops.Dequeue();
            if (operation == null) return;
            Mixpanel.Log($"Dispatching new operation: {operation.GetAction()}");
            Value data = operation.GetWhat();
            switch (operation.GetAction())
            {
                case ThreadOperation.ThreadOperationAction.ENQUEUE_EVENTS:
                    EnqueueMixpanelQueue(MixpanelStorage.TrackPersistentQueue, data);
                    break;
                case ThreadOperation.ThreadOperationAction.ENQUEUE_PEOPLE:
                    EnqueueMixpanelQueue(MixpanelStorage.EngagePersistentQueue, data);
                    break;
                case ThreadOperation.ThreadOperationAction.FLUSH:
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
                case ThreadOperation.ThreadOperationAction.CLEAR_QUEUE:
                    MixpanelStorage.TrackPersistentQueue.Clear();
                    MixpanelStorage.EngagePersistentQueue.Clear();
                    break;
                case ThreadOperation.ThreadOperationAction.KILL_THREAD:
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
                        try {
                            batch.Add(JsonUtility.FromJson<Value>(Encoding.UTF8.GetString(data)));
                        }
                        catch (Exception e) {
                            Mixpanel.LogError($"There was an error processing event [{count}] from the internal object pool: " + e);
                        }
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
                            response = responseRequest.Content.ReadAsStringAsync().Result;
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

        internal static void EnqueueMixpanelQueue(PersistentQueue persistentQueue, Value data)
        {
            using (PersistentQueueSession session = persistentQueue.OpenSession())
            {
                session.Enqueue(Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)));
                Mixpanel.Put(data);
                session.Flush();
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
