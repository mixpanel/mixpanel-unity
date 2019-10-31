using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using mixpanel.queue;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using System.Threading;
using Unity.Jobs;
using Unity.Collections;

namespace mixpanel
{
    internal class MixpanelManager : MonoBehaviour
    {   
        public List<Value> TrackQueue = new List<Value>(500);
        public List<Value> EngageQueue = new List<Value>(500);
        
        #region Singleton
        
        private static MixpanelManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            GetMixpanelInstance();
            Debug.Log($"[Mixpanel] Track Queue Depth: {Mixpanel.TrackQueue.CurrentCountOfItemsInQueue}");
            Debug.Log($"[Mixpanel] Engage Queue Depth: {Mixpanel.EngageQueue.CurrentCountOfItemsInQueue}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            Mixpanel.CollectAutoProperties();
        }
        
        private static MixpanelManager GetMixpanelInstance() {
            if (_instance == null)
            {
                _instance = new GameObject("Mixpanel").AddComponent<MixpanelManager>();
                new GameObject("Mixpanel").AddComponent<MixpanelThread>();
            }
            return _instance;
        }

        #endregion

        void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                Mixpanel.InitSession();
            }
        }

        private IEnumerator Start()
        {
            MigrateFrom1To2();
            DontDestroyOnLoad(this);
            StartCoroutine(PopulatePools());
            MixpanelSettings.LoadSettings();
            TrackIntegrationEvent();
            while (true)
            {
                float flushInterval = MixpanelConfig.FlushInterval;
                Debug.Log("Flush interval " + flushInterval);
                yield return new WaitForSecondsRealtime(flushInterval);
                MixpanelThread.FlushOp();
            }
        }

        private static IEnumerator PopulatePools()
        {
            for (int i = 0; i < MixpanelConfig.PoolFillFrames; i++)
            {
                Mixpanel.NullPool.Put(Value.Null);
                for (int j = 0; j < MixpanelConfig.PoolFillEachFrame; j++)
                {
                    Mixpanel.ArrayPool.Put(Value.Array);
                    Mixpanel.ObjectPool.Put(Value.Object);
                }
                yield return null;
            }
        }

        private void LateUpdate()
        {
            LateUpdateTrackQueue();
            LateUpdateEngageQueue();
        }

        private void LateUpdateTrackQueue()
        {
            if (TrackQueue.Count == 0) return;
            if (Mixpanel.UseCoroutines && !Mixpanel.UseThreads && !Mixpanel.UseThreadPool && !Mixpanel.UseLongRunningWorkerThread)
            {
                int queueCount = TrackQueue.Count;
                PersistentQueueSession session = Mixpanel.TrackQueue.OpenSession();
                for (int itemIdx = 0; itemIdx < queueCount; itemIdx++)
                {
                    Value item = TrackQueue[itemIdx];
                    StartCoroutine(StoreQueueInSession(session, item, itemIdx == queueCount - 1));
                }
                TrackQueue.Clear();
            }
            else if (Mixpanel.UseThreads && !Mixpanel.UseThreadPool && !Mixpanel.UseLongRunningWorkerThread)
            {
                Thread t1 = new Thread(UpdateTrackThread);
                t1.Start();
            }
            else if (Mixpanel.UseThreadPool && !Mixpanel.UseLongRunningWorkerThread)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(UpdateTrackThreadCallBack));
            }
            else if (Mixpanel.UseLongRunningWorkerThread)
            {
                MixpanelThread.EnqueueEventOp();
            }
            else
            {
                MixpanelThread.EnqueueMixpanelQueue(Mixpanel.TrackQueue, TrackQueue);
            }

        }

        private void UpdateTrackThread()
        {
            MixpanelThread.EnqueueMixpanelQueue(Mixpanel.TrackQueue, TrackQueue);
        }

        private void UpdateTrackThreadCallBack(object callback)
        {
            MixpanelThread.EnqueueMixpanelQueue(Mixpanel.TrackQueue, TrackQueue);
        }

        private IEnumerator StoreQueueInSession(PersistentQueueSession session, Value item, bool isLast)
        {
            string itemJson = JsonUtility.ToJson(item);
            yield return new WaitForEndOfFrame();
            byte[] itemBytes = Encoding.UTF8.GetBytes(itemJson);
            yield return new WaitForEndOfFrame();
            session.Enqueue(itemBytes);
            yield return new WaitForEndOfFrame();
            Mixpanel.Put(item);

            if (isLast)
            {
                yield return new WaitForEndOfFrame();
                session.Flush();
                yield return new WaitForEndOfFrame();
                session.Dispose();
                yield return null;
            }
        }

        private void LateUpdateEngageQueue()
        {
            if (EngageQueue.Count == 0) return;
            if (Mixpanel.UseLongRunningWorkerThread)
            {
                MixpanelThread.EnqueuePeopleOp();
            }
            else
            {
                MixpanelThread.EnqueueMixpanelQueue(Mixpanel.EngageQueue, EngageQueue);
            }
        }

        private void DoFlush(string url, PersistentQueue queue)
        {
            lock (queue)
            {
                int depth = queue.CurrentCountOfItemsInQueue;
                int count = (depth / MixpanelConfig.BatchSize) + (depth % MixpanelConfig.BatchSize != 0 ? 1 : 0);
                for (int i = 0; i < count; i++)
                {
                    StartCoroutine(DoRequest(url, queue));
                }
            }
        }

        private IEnumerator DoRequest(string url, PersistentQueue queue, int retryCount = 0)
        {
            int count = 0;
            Value batch = Mixpanel.ArrayPool.Get();
            using (PersistentQueueSession session = queue.OpenSession())
            {
                while (count < MixpanelConfig.BatchSize)
                {
                    byte[] data = session.Dequeue();
                    if (data == null) break;
                    Value s = JsonUtility.FromJson<Value>(Encoding.UTF8.GetString(data));
                    batch.Add(s);
                    Debug.Log(s.ToString());
                    ++count;
                }
                // If the batch is empty don't send the request
                if (count == 0) yield break;
                string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(batch.ToString()));
                if (MixpanelConfig.ShowDebug) {
                    Debug.Log($"[Mixpanel] Sending Request - '{url}' with payload '{payload}'");
                }
                WWWForm form = new WWWForm();
                form.AddField("data", payload);
                UnityWebRequest request = UnityWebRequest.Post(url, form);
                yield return request.SendWebRequest();
                while (!request.isDone) yield return new WaitForEndOfFrame();
                if (MixpanelConfig.ShowDebug) {
                    Debug.Log($"[Mixpanel] Response from request - '{url}':'{request.downloadHandler.text}'");
                }
                if (!request.isNetworkError && !request.isHttpError)
                {
                    session.Flush();
                    Mixpanel.Put(batch);
                    yield break;
                }
                if (retryCount > 10) yield break;
            }
            retryCount += 1;
            // 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 = 2046 seconds total
            yield return new WaitForSecondsRealtime((float)Math.Pow(2, retryCount));
            StartCoroutine(DoRequest(url, queue, retryCount));
        }

        public static void AddTrack(Value item)
        {
            lock (GetMixpanelInstance().TrackQueue)
            {
                GetMixpanelInstance().TrackQueue.Add(item);
            }
            // GetMixpanelInstance().TrackQueue.Add(item);
        }

        public static void AddEngageUpdate(Value item)
        {
            lock (GetMixpanelInstance().EngageQueue)
            {
                GetMixpanelInstance().EngageQueue.Add(item);
            }
            // GetMixpanelInstance().EngageQueue.Add(item);
        }

        #region Static

        internal static void Flush()
        {
            GetMixpanelInstance().LateUpdate();
            GetMixpanelInstance().DoFlush(MixpanelConfig.TrackUrl, Mixpanel.TrackQueue);
            GetMixpanelInstance().DoFlush(MixpanelConfig.EngageUrl, Mixpanel.EngageQueue);
        }
        
        #endregion

        #region InternalSDK

        private void MigrateFrom1To2() {
            if (!Mixpanel.HasMigratedFrom1To2)
            {
                string stateFile = Application.persistentDataPath + "/mp_state.json";
                try
                {
                    if (System.IO.File.Exists(stateFile))
                    {
                        string state = System.IO.File.ReadAllText(stateFile);
                        Value stateValue = Value.Deserialize(state);
                        string distinctIdKey = "distinct_id";
                        if (stateValue.ContainsKey(distinctIdKey) && !stateValue[distinctIdKey].IsNull)
                        {
                            string distinctId = stateValue[distinctIdKey];
                            Mixpanel.DistinctId = distinctId;
                        }
                        string optedOutKey = "opted_out";
                        if (stateValue.ContainsKey(optedOutKey) && !stateValue[optedOutKey].IsNull)
                        {
                            bool optedOut = stateValue[optedOutKey];
                            Mixpanel.IsTracking = !optedOut;
                        }
                        string trackedIntegrationKey = "tracked_integration";
                        if (stateValue.ContainsKey(trackedIntegrationKey) && !stateValue[trackedIntegrationKey].IsNull)
                        {
                            bool trackedIntegration = stateValue[trackedIntegrationKey];
                            Mixpanel.HasIntegratedLibrary = trackedIntegration;
                        }
                    }
                }
                catch (Exception)
                {
                    Debug.LogError("Error migrating state from v1 to v2");
                }
                finally
                {
                    System.IO.File.Delete(stateFile);
                }

                string superPropertiesFile = Application.persistentDataPath + "/mp_super_properties.json";
                try
                {
                    if (System.IO.File.Exists(superPropertiesFile))
                    {
                        string superProperties = System.IO.File.ReadAllText(superPropertiesFile);
                        Value superPropertiesValue = Value.Deserialize(superProperties);
                        foreach (KeyValuePair<string, Value> kvp in superPropertiesValue)
                        {
                            if (!kvp.Key.StartsWith("$"))
                            {
                                Mixpanel.Register(kvp.Key, kvp.Value);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    Debug.LogError("Error migrating super properties from v1 to v2");
                }
                finally
                {
                    System.IO.File.Delete(superPropertiesFile);
                }

                Mixpanel.HasMigratedFrom1To2 = true;
            }
        }

        private void TrackIntegrationEvent()
        {
            if (Mixpanel.HasIntegratedLibrary)
            {
                return;
            }
            string body = "{\"event\":\"Integration\",\"properties\":{\"token\":\"85053bf24bba75239b16a601d9387e17\",\"mp_lib\":\"unity\",\"distinct_id\":\"" + MixpanelSettings.Instance.Token +"\"}}";
            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(body));
            WWWForm form = new WWWForm();
            form.AddField("data", payload);
            UnityWebRequest request = UnityWebRequest.Post("https://api.mixpanel.com/", form);
            StartCoroutine(WaitForIntegrationRequest(request));
        }

        private IEnumerator<UnityWebRequest> WaitForIntegrationRequest(UnityWebRequest request)
        {
            yield return request;
            Mixpanel.HasIntegratedLibrary = true;
        }

        #endregion
    }
}
