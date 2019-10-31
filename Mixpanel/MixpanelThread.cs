using UnityEngine;
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
        private Thread bgThread;

        private static bool stopThread = false;
        private static int retryCount = 0;
        private static System.Threading.Timer retryTimer = null;

        private void Start()
        {
            bgThread = new Thread(RunBackgroundThread);
            bgThread.Start();
        }

        public static void EnqueueEventOp()
        {
            ops.Enqueue(ThreadOperation.ENQUEUE_EVENTS);
        }

        public static void EnqueuePeopleOp()
        {
            ops.Enqueue(ThreadOperation.ENQUEUE_PEOPLE);
        }

        public static void FlushOp()
        {
            if (retryCount > 0) return;
            ForceFlushOp();
        }

        private static void ForceFlushOp()
        {
            ops.Enqueue(ThreadOperation.FLUSH);
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
                    bgThread.Abort(); // Will throw an exception
                    break;
                default:
                    break;
            }
        }

        internal static void SendData(PersistentQueue persistentQueue, string url)
        {
            if (persistentQueue.CurrentCountOfItemsInQueue == 0) return;
            Mixpanel.Log("Items in queue: " + persistentQueue.CurrentCountOfItemsInQueue);
            Value batch = Mixpanel.ArrayPool.Get();
            int depth = persistentQueue.CurrentCountOfItemsInQueue;
            int numBatches = (depth / MixpanelConfig.BatchSize) + (depth % MixpanelConfig.BatchSize != 0 ? 1 : 0);
            for (int i = 0; i < numBatches; i++)
            {
                if (stopThread) return;
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
                    if (count == 0) break;
                    string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(batch.ToString()));
                    Mixpanel.Log($"Sending Request - '{url}' with payload '{payload}'");
                    var content = new StringContent("data=" + payload, Encoding.UTF8, "application/json");
                    bool successful = false;
                    try
                    {
                        var response = client.PostAsync(url, content).Result;
                        Mixpanel.Log($"Response - '{url}' was '{response}'");
                        successful = response.StatusCode == HttpStatusCode.OK;
                    }
                    catch (Exception e)
                    {
                        Mixpanel.LogError("There was an error sending the request: " + e);
                    }
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
                        break;
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
}