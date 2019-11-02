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
    internal class MixpanelThread : MonoBehaviour
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
        private static Queue<ThreadOperation> ops = new Queue<ThreadOperation>();
        private static readonly HttpClient client = new HttpClient();
        private static MixpanelThread _instance = null;
        private Thread bgThread;

        private static bool stopThread = false;
        private static bool isBgThreadRunning = false;
        private static int retryCount = 0;
        private static System.Threading.Timer retryTimer = null;

        private void Start()
        {
            bgThread = new Thread(RunBackgroundThread);
            bgThread.Start();
        }

        private void Awake()
        {
            _instance = this;
        }

        public static void EnqueueEventOp()
        {
            ops.Enqueue(ThreadOperation.ENQUEUE_EVENTS);
            if (!isBgThreadRunning)
            {
                _instance.DispatchOperations();
            }
        }

        public static void EnqueuePeopleOp()
        {
            ops.Enqueue(ThreadOperation.ENQUEUE_PEOPLE);
            if (!isBgThreadRunning)
            {
                _instance.DispatchOperations();
            }
        }

        public static void FlushOp()
        {
            if (retryCount > 0) return;
            ForceFlushOp();
        }

        private static void ForceFlushOp()
        {
            ops.Enqueue(ThreadOperation.FLUSH);
            if (!isBgThreadRunning)
            {
                _instance.DispatchOperations();
            }
        }

        private static void ForceStop()
        {
            ops.Enqueue(ThreadOperation.KILL_THREAD);
        }

        private void OnDisable()
        {
            stopThread = true;
            if (retryTimer != null)
            {
                retryTimer.Dispose();
            }
            ForceStop();
        }

        private void RunBackgroundThread()
        {
            isBgThreadRunning = true;
            while (!stopThread)
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
            isBgThreadRunning = false;
        }

// Dispose / MonoBehavior
// Change name
        private void DispatchOperations()
        {
            if (ops.Count == 0) return;
            ThreadOperation operation = ops.Dequeue();
            switch (operation)
            {
                case ThreadOperation.ENQUEUE_EVENTS:
                    EnqueueMixpanelQueue(Mixpanel.TrackQueue, MixpanelManager.GetMixpanelInstance().TrackQueue);
                    break;
                case ThreadOperation.ENQUEUE_PEOPLE:
                    EnqueueMixpanelQueue(Mixpanel.EngageQueue, MixpanelManager.GetMixpanelInstance().EngageQueue);
                    break;
                case ThreadOperation.FLUSH:
                    SendData(Mixpanel.TrackQueue, MixpanelConfig.TrackUrl);
                    SendData(Mixpanel.EngageQueue, MixpanelConfig.EngageUrl);
                    break;
                case ThreadOperation.KILL_THREAD:
                    isBgThreadRunning = false;
                    bgThread.Abort(); // Will throw an exception
                    break;
                default:
                    break;
            }
        }

        internal void SendData(PersistentQueue persistentQueue, string url)
        {
            if (persistentQueue.CurrentCountOfItemsInQueue == 0) return;
            Mixpanel.Log("Items in queue: " + persistentQueue.CurrentCountOfItemsInQueue);
            
            int depth = persistentQueue.CurrentCountOfItemsInQueue;
            int numBatches = (depth / MixpanelConfig.BatchSize) + (depth % MixpanelConfig.BatchSize != 0 ? 1 : 0);
            for (int i = 0; i < numBatches; i++)
            {
                if (stopThread) return;
                if (isBgThreadRunning)
                {
                    DoRequest(persistentQueue, url);
                }
                else
                {
                    StartCoroutine(DoRequest(persistentQueue, url));
                }                        
            }
        }

        private IEnumerator DoRequest(PersistentQueue persistentQueue, string url)
        {
            Value batch = Mixpanel.ArrayPool.Get();
            using (PersistentQueueSession session = persistentQueue.OpenSession())
            {
                int count = 0;
                while (count < MixpanelConfig.BatchSize)
                {
                    byte[] data = session.Dequeue();
                    if (data == null) break;
                    batch.Add(JsonUtility.FromJson<Value>(Encoding.UTF8.GetString(data)));
                    ++count;
                }
                if (count > 0) yield break;
                string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(batch.ToString()));
                Mixpanel.Log($"Sending Request - '{url}' with payload '{payload}'");
                bool successful = false;
                int responseCode = -1;
                string response = null;
                if (isBgThreadRunning)
                {
                    try
                    {
                        var content = new StringContent("data=" + payload, Encoding.UTF8, "application/json");
                        var responseRequest = client.PostAsync(url, content).Result;
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
                    retryCount = 0;
                    Mixpanel.Log("Flushing session");
                    session.Flush();
                    Mixpanel.Put(batch);
                }
                else
                {
                    retryCount += 1;
                    int retryIn = (int) Math.Pow(2, retryCount) * 60000;
                    retryIn = Math.Min(retryIn, 10 * 60 * 1000); // limit 10 min
                    Mixpanel.Log("Retrying request in " + retryIn / 1000 + " seconds");
                    retryTimer = new System.Threading.Timer((obj) =>
                    {
                        ForceFlushOp();
                        retryTimer.Dispose();
                    }, null, retryIn, System.Threading.Timeout.Infinite);
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
}