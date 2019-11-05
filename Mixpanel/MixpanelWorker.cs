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
    public static partial class Mixpanel
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
        private static Value _autoTrackProperties;
        private static Value _autoEngageProperties;
        private static Int32 _eventCounter = 0, _peopleCounter = 0, _sessionStartEpoch;
        private static String _sessionID;

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

        public static void EnqueueEventOp()
        {
            _ops.Enqueue(ThreadOperation.ENQUEUE_EVENTS);
            if (!_isBgThreadRunning)
            {
                DispatchOperations();
            }
        }

        public static void EnqueuePeopleOp()
        {
            _ops.Enqueue(ThreadOperation.ENQUEUE_PEOPLE);
            if (!_isBgThreadRunning)
            {
                DispatchOperations();
            }
        }

        public static void FlushOp()
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

        private static void ForceStop()
        {
            _ops.Enqueue(ThreadOperation.KILL_THREAD);
        }

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
                    EnqueueMixpanelQueue(Mixpanel.TrackQueue, MixpanelManager.GetMixpanelInstance().TrackQueue);
                    break;
                case ThreadOperation.ENQUEUE_PEOPLE:
                    EnqueueMixpanelQueue(Mixpanel.EngageQueue, MixpanelManager.GetMixpanelInstance().EngageQueue);
                    break;
                case ThreadOperation.FLUSH:
                    if (_isBgThreadRunning)
                    {
                        IEnumerator trackEnum = SendData(Mixpanel.TrackQueue, MixpanelConfig.TrackUrl);
                        IEnumerator engageEnum = SendData(Mixpanel.EngageQueue, MixpanelConfig.EngageUrl);
                        while (trackEnum.MoveNext()) {};
                        while (engageEnum.MoveNext()) {};
                    }
                    else
                    {
                        MixpanelManager.GetMixpanelInstance().StartCoroutine(SendData(Mixpanel.TrackQueue, MixpanelConfig.TrackUrl));
                        MixpanelManager.GetMixpanelInstance().StartCoroutine(SendData(Mixpanel.EngageQueue, MixpanelConfig.EngageUrl));
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
            Mixpanel.Log("Items in queue: " + persistentQueue.CurrentCountOfItemsInQueue);
            
            int depth = persistentQueue.CurrentCountOfItemsInQueue;
            int numBatches = (depth / MixpanelConfig.BatchSize) + (depth % MixpanelConfig.BatchSize != 0 ? 1 : 0);
            for (int i = 0; i < numBatches; i++)
            {
                if (_stopThread) yield break;
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

        private static Value GetEventMetadata() {
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

        private static Value GetPeopleMetadata() {
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

        internal static void InitSession() {
            _eventCounter = 0;
            _peopleCounter = 0;
            _sessionID = Convert.ToString(UnityEngine.Random.Range(0, Int32.MaxValue), 16);
            _sessionStartEpoch = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        private static void DoTrack(string eventName, Value properties)
        {
            if (!IsTracking) return;
            if (properties == null) properties = ObjectPool.Get();
            properties.Merge(GetEventsDefaultProperties());
            // These auto properties can change in runtime so we don't bake them into AutoProperties
            properties["$screen_width"] = Screen.width;
            properties["$screen_height"] = Screen.height;
            properties.Merge(OnceProperties);
            ResetOnceProperties();
            properties.Merge(SuperProperties);
            if (TimedEvents.TryGetValue(eventName, out Value startTime))
            {
                properties["$duration"] = CurrentTime() - (double)startTime;
                TimedEvents.Remove(eventName);
            }
            properties["token"] = MixpanelSettings.Instance.Token;
            properties["distinct_id"] = DistinctId;
            properties["time"] = CurrentTime();
            Value data = ObjectPool.Get();
            data["event"] = eventName;
            data["properties"] = properties;
            data["$mp_metadata"] = GetEventMetadata();
            MixpanelManager.AddTrack(data);
        }

        private static void DoEngage(Value properties)
        {
            if (!IsTracking) return;
            properties["$token"] = MixpanelSettings.Instance.Token;
            properties["$distinct_id"] = DistinctId;
            properties["$time"] = CurrentTime();
            properties["$mp_metadata"] = GetPeopleMetadata();
            MixpanelManager.AddEngageUpdate(properties);
        }

        internal static void CollectAutoProperties()
        {
            GetEngageDefaultProperties();
            GetEventsDefaultProperties();
        }

        internal static Value GetEngageDefaultProperties() {
            if (_autoEngageProperties == null) {
                Value properties = new Value();
                    #if UNITY_IOS
                        properties["$ios_lib_version"] = MixpanelUnityVersion;
                        properties["$ios_version"] = Device.systemVersion;
                        properties["$ios_app_release"] = Application.version;
                        properties["$ios_device_model"] = SystemInfo.deviceModel;
                        properties["$ios_ifa"] = Device.advertisingIdentifier;
                        // properties["$ios_app_version"] = Application.version;
                    #elif UNITY_ANDROID
                        properties["$android_lib_version"] = MixpanelUnityVersion;
                        properties["$android_os"] = "Android";
                        properties["$android_os_version"] = SystemInfo.operatingSystem;
                        properties["$android_model"] = SystemInfo.deviceModel;
                        properties["$android_app_version"] = Application.version;
                        // properties["$android_manufacturer"] = "";
                        // properties["$android_brand"] = "";
                        // properties["$android_app_version_code"] = Application.version;
                    #else
                        properties["$lib_version"] = MixpanelUnityVersion;
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
                    {"$lib_version", MixpanelUnityVersion},
                    {"$os", SystemInfo.operatingSystemFamily.ToString()},
                    {"$os_version", SystemInfo.operatingSystem},
                    {"$model", SystemInfo.deviceModel},
                    {"$app_version_string", Application.version},
                    {"$wifi", Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork},
                    {"$radio", GetRadio()},
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

        private static string GetRadio()
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

        private static double CurrentTime()
        {
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            double currentEpochTime = (DateTime.UtcNow - epochStart).TotalSeconds;
            return currentEpochTime;
        }

        private static string CurrentDateTime()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        }
    }
}
