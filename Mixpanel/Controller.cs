using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using System.Threading;
using Unity.Jobs;
using Unity.Collections;
using System.Net;
using System.Net.Http;

#if UNITY_IOS
using UnityEngine.iOS;
#endif

namespace mixpanel
{
    internal class Controller : MonoBehaviour
    {
        private static Value _autoTrackProperties;
        private static Value _autoEngageProperties;

        private static int _retryCount = 0;
        private static DateTime _retryTime;

        #region Singleton

        private static Controller _instance;
        private static bool _fullyInitialized = false;
        private static bool _isFlushCoroutineRunning = false;
        private static bool _isInitializing = false;  // Guard against concurrent initialization

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            MixpanelSettings.LoadSettings();
            if (Config.ManualInitialization) return;
            Initialize();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            // Formerly initialized auto properties here; now handled synchronously in Initialize()
            // Kept as placeholder for potential future use
        }

        internal static void Initialize() {
            // Guard against concurrent initialization calls
            if (_isInitializing) {
                Mixpanel.Log($"Mixpanel initialization already in progress");
                return;
            }
            if (_fullyInitialized) {
                Mixpanel.Log($"Mixpanel already initialized");
                return;
            }

            _isInitializing = true;

            try {
                // Copy over any runtime changes that happened before initialization from settings instance to the config.
                MixpanelSettings.Instance.ApplyToConfig();

                // Create the singleton instance
                Controller instance = GetInstance();

                // SYNCHRONOUS INITIALIZATION - Critical operations that must complete before tracking
                // 1. Initialize session metadata BEFORE any tracking can occur
                Metadata.InitSession();

                // 2. Migrate v1 data if needed (sets DistinctId and IsTracking from old SDK)
                instance.MigrateFrom1To2();

                // 3. Initialize auto properties synchronously to ensure they're ready for first event
                GetEventsDefaultProperties();
                GetEngageDefaultProperties();

                // 4. Eagerly load persisted properties to ensure consistent Track() performance
                // This prevents lazy-loading with disk I/O when Track() is called immediately after Init()
                // Moving the I/O cost from first Track() to Init() for predictable performance
                PreloadPersistedProperties();

                // Note: Flush coroutine will be started in Start() to follow Unity lifecycle best practices
                // and prevent duplicate coroutines if initialization partially fails

                // Mark as fully initialized
                _fullyInitialized = true;

                Mixpanel.Log($"Mixpanel fully initialized (synchronous)");
            }
            catch (Exception e) {
                Mixpanel.LogError($"Error during Mixpanel initialization: {e}");
                _fullyInitialized = false; // Ensure we're not marked as initialized on error
                // Note: Start() will attempt fallback initialization if _fullyInitialized remains false
            }
            finally {
                _isInitializing = false; // Always clear the initialization flag
            }
        }

        internal static bool IsInitialized() {
            return _instance != null && _fullyInitialized;
        }

        internal static void Disable() {
            if (_instance != null) {
                Destroy(_instance);
                _instance = null; // Prevent race condition by clearing reference immediately
                ResetStaticState(); // Centralize cleanup logic
                Metadata.ResetSession();  // Clear session state for clean re-initialization
            }
        }

        internal static Controller GetInstance()
        {
            if (_instance == null)
            {
                GameObject g = new GameObject ("Mixpanel");
                _instance = g.AddComponent<Controller>();
                DontDestroyOnLoad(g);
            }
            return _instance;
        }

        #endregion

        void OnDestroy()
        {
            Mixpanel.Log($"Mixpanel Component Destroyed");

            // Only clear the static reference if this is the actual singleton instance
            if (_instance == this)
            {
                ResetStaticState();
            }
        }

        /// <summary>
        /// Resets all static state for the Controller singleton.
        /// Should only be called from static context or by the singleton instance.
        /// </summary>
        internal static void ResetStaticState()
        {
            _instance = null;  // Clear static reference to destroyed instance
            _fullyInitialized = false;  // Reset initialization state
            _isFlushCoroutineRunning = false;  // Reset coroutine flag
            _isInitializing = false;  // Reset initialization guard
            _autoTrackProperties = null;  // Reset auto-properties cache
            _autoEngageProperties = null; // Reset auto-properties cache
            Metadata.ResetSession();  // Clear session state
        }

        /// <summary>
        /// Helper method to set the fully initialized flag from instance methods.
        /// This avoids direct static field writes from instance methods.
        /// </summary>
        private static void SetFullyInitialized(bool value)
        {
            _fullyInitialized = value;
        }

        /// <summary>
        /// Helper method to set the flush coroutine running flag from instance methods.
        /// This avoids direct static field writes from instance methods.
        /// </summary>
        private static void SetFlushCoroutineRunning(bool value)
        {
            _isFlushCoroutineRunning = value;
        }

        void OnApplicationPause(bool pauseStatus)
        {
            // On resume, ensure session is initialized even if initialization was interrupted
            if (!pauseStatus)
            {
                if (!Metadata.IsSessionInitialized())
                {
                    Metadata.InitSession();
                }
            }
        }

        private void Start()
        {
            // All initialization now happens synchronously in Initialize()
            // This method is called by Unity after the GameObject is created
            // but all critical initialization has already completed

            // Start the flush coroutine if initialized and not already running
            if (_fullyInitialized && !_isFlushCoroutineRunning)
            {
                SetFlushCoroutineRunning(true);
                StartCoroutine(WaitAndFlush());
                Mixpanel.Log($"Mixpanel flush coroutine started");
            }
            // Safety check with active fallback for edge cases (e.g., GameObject created manually)
            else if (!_fullyInitialized)
            {
                Mixpanel.LogError("Mixpanel Component Start() called without proper initialization. Attempting fallback initialization...");
                try
                {
                    // Check if session was already initialized to avoid resetting counters
                    bool sessionAlreadyInitialized = Metadata.IsSessionInitialized();

                    // Apply settings if not already done
                    MixpanelSettings.Instance.ApplyToConfig();

                    // Only initialize session if not already done
                    if (!sessionAlreadyInitialized)
                    {
                        Metadata.InitSession();
                    }
                    else
                    {
                        Mixpanel.Log($"Preserving existing session during fallback initialization");
                    }

                    // Migrate if not already done
                    MigrateFrom1To2();

                    // Initialize auto properties
                    GetEventsDefaultProperties();
                    GetEngageDefaultProperties();

                    // Preload persisted properties
                    PreloadPersistedProperties();

                    // Mark as initialized
                    SetFullyInitialized(true);

                    // Start flush coroutine after initialization
                    if (!_isFlushCoroutineRunning)
                    {
                        SetFlushCoroutineRunning(true);
                        StartCoroutine(WaitAndFlush());
                        Mixpanel.Log($"Mixpanel flush coroutine started (fallback)");
                    }

                    Mixpanel.Log($"Mixpanel fallback initialization completed successfully");
                }
                catch (Exception e)
                {
                    Mixpanel.LogError($"Mixpanel fallback initialization failed: {e}");
                    // SDK remains non-functional but won't crash the app
                }
            }

            Mixpanel.Log($"Mixpanel Component Started");
        }


        private IEnumerator WaitAndFlush()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(Config.FlushInterval);
                DoFlush();
            }
        }

        internal void DoFlush(Action<bool> onFlushComplete = null)
        {
            int coroutinesCount = 2; // Number of coroutines to wait for
            bool overallSuccess = true;

            Action<bool> onComplete = onFlushComplete != null ?
                new Action<bool>(success => {
                    overallSuccess &= success;
                    CheckCompletion(onFlushComplete, ref coroutinesCount, overallSuccess);
                })
                : (Action<bool>)null;
            StartCoroutine(SendData(MixpanelStorage.FlushType.EVENTS, onComplete));
            StartCoroutine(SendData(MixpanelStorage.FlushType.PEOPLE, onComplete));
        }

        private IEnumerator SendData(MixpanelStorage.FlushType flushType, Action<bool> onComplete)
        {
            if (_retryTime > DateTime.Now && _retryCount > 0)
            {
                onComplete?.Invoke(false);
                yield break;
            }

            string url = (flushType == MixpanelStorage.FlushType.EVENTS) ? Config.TrackUrl : Config.EngageUrl;
            Value batch = MixpanelStorage.DequeueBatchTrackingData(flushType, Config.BatchSize);
            while (batch.Count > 0) {
                WWWForm form = new WWWForm();
                String payload = batch.ToString();
                form.AddField("data", payload);
                Mixpanel.Log("Sending batch of data: " + payload);
                using (UnityWebRequest request = UnityWebRequest.Post(url, form))
                {
                    yield return request.SendWebRequest();
                    #if UNITY_2020_1_OR_NEWER
                    if (request.result != UnityWebRequest.Result.Success)
                    #else
                    if (request.isHttpError || request.isNetworkError)
                    #endif
                    {
                        Mixpanel.Log("API request to " + url + "has failed with reason " + request.error);
                        _retryCount += 1;
                        double retryIn = Math.Pow(2, _retryCount - 1) * 60;
                        retryIn = Math.Min(retryIn, 10 * 60); // limit 10 min
                        _retryTime = DateTime.Now;
                        _retryTime = _retryTime.AddSeconds(retryIn);
                        Mixpanel.Log("Retrying request in " + retryIn + " seconds (retryCount=" + _retryCount + ")");
                        onComplete?.Invoke(false);
                        yield break;
                    }
                    else
                    {
                        _retryCount = 0;
                        MixpanelStorage.DeleteBatchTrackingData(batch);
                        batch = MixpanelStorage.DequeueBatchTrackingData(flushType, Config.BatchSize);
                        Mixpanel.Log("Successfully posted to " + url);
                    }
                }
            }

            onComplete?.Invoke(true);
        }

        private void CheckCompletion(Action<bool> onFlushComplete, ref int coroutinesCount, bool overallSuccess)
        {
            // Decrease the counter
            coroutinesCount--;

            // If all coroutines are finished, invoke the onFlushComplete callback
            if (coroutinesCount == 0)
            {
                onFlushComplete?.Invoke(overallSuccess);
            }
        }

        private IEnumerator SendHttpEvent(string eventName, string apiToken, string distinctId, string properties, bool updatePeople)
        {
            string body = "{\"event\":\"" + eventName + "\",\"properties\":{\"token\":\"" +
                        apiToken + "\",\"DevX\":true,\"mp_lib\":\"unity\"," +
                        "\"$lib_version\":\"" + Mixpanel.MixpanelUnityVersion + "\"," +
                        "\"Project Token\":\"" + distinctId + "\",\"distinct_id\":\"" + distinctId + "\"" + properties + "}}";
            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(body));
            WWWForm form = new WWWForm();
            form.AddField("data", payload);

            using (UnityWebRequest request = UnityWebRequest.Post(Config.TrackUrl, form)) {
                yield return request.SendWebRequest();
            }

            if (updatePeople) {
                body = "{\"$add\":" + "{\"" + eventName +
                "\":1}," +
                            "\"$token\":\"" + apiToken + "\"," +
                            "\"$distinct_id\":\"" + distinctId + "\"}";
                payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(body));
                form = new WWWForm();
                form.AddField("data", payload);

                using (UnityWebRequest request = UnityWebRequest.Post(Config.EngageUrl, form)) {
                    yield return request.SendWebRequest();
                }
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
                    Mixpanel.LogError("Error migrating super properties from v1 to v2");
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
                    #elif UNITY_ANDROID
                        properties["$android_lib_version"] = Mixpanel.MixpanelUnityVersion;
                        properties["$android_os"] = "Android";
                        properties["$android_os_version"] = SystemInfo.operatingSystem;
                        properties["$android_model"] = SystemInfo.deviceModel;
                        properties["$android_app_version"] = Application.version;
                    #else
                        properties["$lib_version"] = Mixpanel.MixpanelUnityVersion;
                    #endif
                _autoEngageProperties = properties;
            }
            return _autoEngageProperties;
        }

        private static void PreloadPersistedProperties() {
            // Eagerly load all persisted properties from disk into memory cache
            // This prevents lazy-loading with disk I/O during the first Track() call
            // Each property getter will check if cached, and if not, load from PlayerPreferences
            try {
                _ = MixpanelStorage.SuperProperties;   // Force load + cache
                _ = MixpanelStorage.OnceProperties;    // Force load + cache
                _ = MixpanelStorage.TimedEvents;       // Force load + cache
                Mixpanel.Log($"Preloaded persisted properties from storage");
            }
            catch (Exception e) {
                // Non-critical failure - properties will lazy-load on first use
                Mixpanel.LogError($"Failed to preload persisted properties: {e}");
            }
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
                };
                #if UNITY_IOS
                    properties["$os"] = "Apple";
                    properties["$os_version"] = Device.systemVersion;
                    properties["$manufacturer"] = "Apple";
                #endif
                #if UNITY_ANDROID
                    properties["$os"] = "Android";
                #endif
                _autoTrackProperties = properties;
            }
            return _autoTrackProperties;
        }

        internal static void DoTrack(string eventName, Value properties)
        {
            if (!MixpanelStorage.IsTracking) return;
            if (properties == null) properties = new Value();
            properties.Merge(GetEventsDefaultProperties());
            // These auto properties can change in runtime so we don't bake them into AutoProperties
            properties["$screen_width"] = Screen.width;
            properties["$screen_height"] = Screen.height;
            properties.Merge(MixpanelStorage.OnceProperties);
            properties.Merge(MixpanelStorage.SuperProperties);
            Value startTime;
            if (MixpanelStorage.TimedEvents.TryGetValue(eventName, out startTime))
            {
                properties["$duration"] = Util.CurrentTimeInSeconds() - (double)startTime;
                MixpanelStorage.TimedEvents.Remove(eventName);
            }
            properties["token"] = MixpanelSettings.Instance.Token;
            properties["distinct_id"] = MixpanelStorage.DistinctId;
            properties["time"] = Util.CurrentTimeInMilliseconds();

            Value data = new Value();

            data["event"] = eventName;
            data["properties"] = properties;
            data["$mp_metadata"] = Metadata.GetEventMetadata();

            MixpanelStorage.EnqueueTrackingData(data, MixpanelStorage.FlushType.EVENTS);
        }

        internal static void DoEngage(Value properties)
        {
            if (!MixpanelStorage.IsTracking) return;
            properties["$token"] = MixpanelSettings.Instance.Token;
            properties["$distinct_id"] = MixpanelStorage.DistinctId;
            properties["$time"] = Util.CurrentTimeInMilliseconds();
            properties["$mp_metadata"] = Metadata.GetPeopleMetadata();

            MixpanelStorage.EnqueueTrackingData(properties, MixpanelStorage.FlushType.PEOPLE);
        }

        internal static void DoClear()
        {
            MixpanelStorage.DeleteAllTrackingData(MixpanelStorage.FlushType.EVENTS);
            MixpanelStorage.DeleteAllTrackingData(MixpanelStorage.FlushType.PEOPLE);
        }

        #endregion

        internal static class Metadata
        {
            private static Int32 _eventCounter = 0, _peopleCounter = 0, _sessionStartEpoch;
            private static String _sessionID;
            private static System.Random _random = new System.Random(Guid.NewGuid().GetHashCode());
            private static bool _sessionInitialized = false;

            internal static bool IsSessionInitialized() {
                return _sessionInitialized;
            }

            internal static void InitSession() {
                _eventCounter = 0;
                _peopleCounter = 0;
                _sessionID = Convert.ToString(_random.Next(0, Int32.MaxValue), 16);
                _sessionStartEpoch = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                _sessionInitialized = true;
            }

            /// <summary>
            /// Resets session state for clean re-initialization.
            /// Should be called when the SDK is disabled or destroyed.
            /// </summary>
            internal static void ResetSession() {
                _sessionInitialized = false;
                _eventCounter = 0;
                _peopleCounter = 0;
                _sessionID = null;
                _sessionStartEpoch = 0;
            }
            internal static Value GetEventMetadata() {
                Value eventMetadata = new Value
                {
                    {"$mp_event_id", Convert.ToString(_random.Next(0, Int32.MaxValue), 16)},
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
                    {"$mp_event_id", Convert.ToString(_random.Next(0, Int32.MaxValue), 16)},
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
