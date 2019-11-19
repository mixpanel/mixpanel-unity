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

#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace mixpanel
{
    internal class Controller : MonoBehaviour
    {
        private static Value _autoTrackProperties;
        private static Value _autoEngageProperties;
        
        #region Singleton
        
        private static Controller _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            GetInstance();
            Mixpanel.Log($"Track Queue Depth: {MixpanelStorage.TrackPersistentQueue.CurrentCountOfItemsInQueue}");
            Mixpanel.Log($"Engage Queue Depth: {MixpanelStorage.EngagePersistentQueue.CurrentCountOfItemsInQueue}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            GetEngageDefaultProperties();
            GetEventsDefaultProperties();
        }
        
        internal static Controller GetInstance()
        {
            if (_instance == null)
            {
                _instance = new GameObject("Mixpanel").AddComponent<Controller>();
            }
            return _instance;
        }

        #endregion

        void OnDestroy()
        {
            Worker.StopWorkerThread();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                Metadata.InitSession();
            }
        }

        private IEnumerator Start()
        {
            MigrateFrom1To2();
            DontDestroyOnLoad(this);
            StartCoroutine(PopulatePools());
            MixpanelSettings.LoadSettings();
            Worker.StartWorkerThread();
            TrackIntegrationEvent();
            while (true)
            {
                yield return new WaitForSecondsRealtime(Config.FlushInterval);
                DoFlush();
            }
        }

        private void TrackIntegrationEvent()
        {
            if (MixpanelStorage.HasIntegratedLibrary) return;
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
            MixpanelStorage.HasIntegratedLibrary = true;
        }

        private static IEnumerator PopulatePools()
        {
            for (int i = 0; i < Config.PoolFillFrames; i++)
            {
                Mixpanel.NullPool.Put(Value.Null);
                for (int j = 0; j < Config.PoolFillEachFrame; j++)
                {
                    Mixpanel.ArrayPool.Put(Value.Array);
                    Mixpanel.ObjectPool.Put(Value.Object);
                }
                yield return null;
            }
        }

        #region InternalSDK

        private void MigrateFrom1To2() {
            if (!MixpanelStorage.HasMigratedFrom1To2)
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
                            MixpanelStorage.DistinctId = distinctId;
                        }
                        string optedOutKey = "opted_out";
                        if (stateValue.ContainsKey(optedOutKey) && !stateValue[optedOutKey].IsNull)
                        {
                            bool optedOut = stateValue[optedOutKey];
                            MixpanelStorage.IsTracking = !optedOut;
                        }
                        string trackedIntegrationKey = "tracked_integration";
                        if (stateValue.ContainsKey(trackedIntegrationKey) && !stateValue[trackedIntegrationKey].IsNull)
                        {
                            bool trackedIntegration = stateValue[trackedIntegrationKey];
                            MixpanelStorage.HasIntegratedLibrary = trackedIntegration;
                        }
                    }
                }
                catch (Exception)
                {
                    Mixpanel.LogError("Error migrating state from v1 to v2");
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

                MixpanelStorage.HasMigratedFrom1To2 = true;
            }
        }

        internal static Value GetEngageDefaultProperties() {
            if (_autoEngageProperties == null) {
                Value properties = new Value();
                    #if UNITY_IOS
                        properties["$ios_lib_version"] = Mixpanel.MixpanelUnityVersion;
                        properties["$ios_version"] = Device.systemVersion;
                        properties["$ios_app_release"] = Application.version;
                        properties["$ios_device_model"] = SystemInfo.deviceModel;
                        properties["$ios_ifa"] = Device.advertisingIdentifier;
                        // properties["$ios_app_version"] = Application.version;
                    #elif UNITY_ANDROID
                        properties["$android_lib_version"] = Mixpanel.MixpanelUnityVersion;
                        properties["$android_os"] = "Android";
                        properties["$android_os_version"] = SystemInfo.operatingSystem;
                        properties["$android_model"] = SystemInfo.deviceModel;
                        properties["$android_app_version"] = Application.version;
                        // properties["$android_manufacturer"] = "";
                        // properties["$android_brand"] = "";
                        // properties["$android_app_version_code"] = Application.version;
                    #else
                        properties["$lib_version"] = Mixpanel.MixpanelUnityVersion;
                    #endif
                _autoEngageProperties = properties;
            }
            return _autoEngageProperties;
        }

        private static Value GetEventsDefaultProperties()
        {
            if (_autoTrackProperties == null) {
                Value properties = new Value
                {
                    {"mp_lib", "unity"},
                    {"$lib_version", Mixpanel.MixpanelUnityVersion},
                    {"$os", SystemInfo.operatingSystemFamily.ToString()},
                    {"$os_version", SystemInfo.operatingSystem},
                    {"$model", SystemInfo.deviceModel},
                    {"$app_version_string", Application.version},
                    {"$wifi", Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork},
                    {"$radio", Util.GetRadio()},
                    {"$device", Application.platform.ToString()},
                    {"$screen_dpi", Screen.dpi},
                    // {"$app_build_number", Application.version},
                    // {"$manufacturer", ""},
                    // {"$carrier", ""},
                    // {"$brand", ""},
                    // {"$has_nfc", false},
                    // {"$has_telephone", false},
                    // {"$bluetooth_enabled", false},
                    // {"$bluetooth_version", "none"}
                };
                #if UNITY_IOS
                    properties["$os"] = "Apple";
                    properties["$os_version"] = Device.systemVersion;
                    properties["$manufacturer"] = "Apple";
                    properties["$ios_ifa"] = Device.advertisingIdentifier;
                #endif
                #if UNITY_ANDROID
                    properties["$os"] = "Android";
                    // properties["$google_play_services"] = "";
                #endif
                _autoTrackProperties = properties;
            }
            return _autoTrackProperties;
        }

        internal static void DoTrack(string eventName, Value properties)
        {
            if (!MixpanelStorage.IsTracking) return;
            if (properties == null) properties = Mixpanel.ObjectPool.Get();
            properties.Merge(GetEventsDefaultProperties());
            // These auto properties can change in runtime so we don't bake them into AutoProperties
            properties["$screen_width"] = Screen.width;
            properties["$screen_height"] = Screen.height;
            properties.Merge(MixpanelStorage.OnceProperties);
            MixpanelStorage.ResetOnceProperties();
            properties.Merge(MixpanelStorage.SuperProperties);
            if (MixpanelStorage.TimedEvents.TryGetValue(eventName, out Value startTime))
            {
                properties["$duration"] = Util.CurrentTime() - (double)startTime;
                MixpanelStorage.TimedEvents.Remove(eventName);
            }
            properties["token"] = MixpanelSettings.Instance.Token;
            properties["distinct_id"] = MixpanelStorage.DistinctId;
            properties["time"] = Util.CurrentTime();
            Value data = Mixpanel.ObjectPool.Get();
            data["event"] = eventName;
            data["properties"] = properties;
            data["$mp_metadata"] = Metadata.GetEventMetadata();

            Worker.EnqueueEventOp(data);
        }

        internal static void DoEngage(Value properties)
        {
            if (!MixpanelStorage.IsTracking) return;
            properties["$token"] = MixpanelSettings.Instance.Token;
            properties["$distinct_id"] = MixpanelStorage.DistinctId;
            properties["$time"] = Util.CurrentTime();
            properties["$mp_metadata"] = Metadata.GetPeopleMetadata();

            Worker.EnqueuePeopleOp(properties);
        }

        internal static void DoFlush()
        {
            Worker.FlushOp();
        }

        internal static void DoClear()
        {
            Worker.ClearOp();
        }

        #endregion

        internal static class Metadata
        {
            private static Int32 _eventCounter = 0, _peopleCounter = 0, _sessionStartEpoch;
            private static String _sessionID;

            internal static void InitSession() {
                _eventCounter = 0;
                _peopleCounter = 0;
                _sessionID = Convert.ToString(UnityEngine.Random.Range(0, Int32.MaxValue), 16);
                _sessionStartEpoch = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            }
            internal static Value GetEventMetadata() {
                Value eventMetadata = new Value
                {
                    {"$mp_event_id", Convert.ToString(UnityEngine.Random.Range(0, Int32.MaxValue), 16)},
                    {"$mp_session_id", _sessionID},
                    {"$mp_session_seq_id", _eventCounter},
                    {"$mp_session_start_sec", _sessionStartEpoch}
                };
                _eventCounter++;
                return eventMetadata;
            }

            internal static Value GetPeopleMetadata() {
                Value peopleMetadata = new Value
                {
                    {"$mp_event_id", Convert.ToString(UnityEngine.Random.Range(0, Int32.MaxValue), 16)},
                    {"$mp_session_id", _sessionID},
                    {"$mp_session_seq_id", _peopleCounter},
                    {"$mp_session_start_sec", _sessionStartEpoch}
                };
                _peopleCounter++;
                return peopleMetadata;

            }
        }
    }
}
