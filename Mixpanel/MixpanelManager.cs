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
            Mixpanel.Log($"[Mixpanel] Track Queue Depth: {Mixpanel.TrackQueue.CurrentCountOfItemsInQueue}");
            Mixpanel.Log($"[Mixpanel] Engage Queue Depth: {Mixpanel.EngageQueue.CurrentCountOfItemsInQueue}");
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
            }
            return _instance;
        }

        #endregion

        void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                MixpanelWorker.InitSession();
            }
        }

        private IEnumerator Start()
        {
            MigrateFrom1To2();
            DontDestroyOnLoad(this);
            StartCoroutine(PopulatePools());
            MixpanelSettings.LoadSettings();
            MixpanelWorker.StartWorkerThread();
            TrackIntegrationEvent();
            while (true)
            {
                yield return new WaitForSecondsRealtime(MixpanelConfig.FlushInterval);
                MixpanelWorker.FlushOp();
            }
        }

        private void TrackIntegrationEvent()
        {
            if (Mixpanel.HasIntegratedLibrary) return;
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
                Debug.Log("Using thread...");
                MixpanelWorker.EnqueueEventOp();
            }
            else
            {
                MixpanelWorker.EnqueueMixpanelQueue(Mixpanel.TrackQueue, TrackQueue);
            }
        }

        private void UpdateTrackThread()
        {
            MixpanelWorker.EnqueueMixpanelQueue(Mixpanel.TrackQueue, TrackQueue);
        }

        private void UpdateTrackThreadCallBack(object callback)
        {
            MixpanelWorker.EnqueueMixpanelQueue(Mixpanel.TrackQueue, TrackQueue);
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
                MixpanelWorker.EnqueuePeopleOp();
            }
            else
            {
                MixpanelWorker.EnqueueMixpanelQueue(Mixpanel.EngageQueue, EngageQueue);
            }
        }

        #region Static

        public static void AddTrack(Value item)
        {
            lock (GetMixpanelInstance().TrackQueue)
            {
                GetMixpanelInstance().TrackQueue.Add(item);
            }
        }

        public static void AddEngageUpdate(Value item)
        {
            lock (GetMixpanelInstance().EngageQueue)
            {
                GetMixpanelInstance().EngageQueue.Add(item);
            }
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
