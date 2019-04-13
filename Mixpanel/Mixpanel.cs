using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace mixpanel
{
    public static partial class Mixpanel
    {
        internal static MixpanelAsync async;
        internal static Value OnceProperties = new Value();
        internal static Value AutoProperties;

        internal static string Base64Encode(string text) {
            var bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }

        internal static string BuildURL(string endpoint, Value data)
        {
            if (MixpanelSettings.Instance.ShowDebug) Debug.Log(Json.Serialize(data));
            return string.Format("{0}/?ip=1&data={1}", endpoint, Base64Encode(Json.Serialize(data)));
        }

        internal static void track(string eventName, Value properties)
        {
            if (!IsTracking) return;
            if (AutoProperties == null) AutoProperties = collectAutoProperties();
            foreach (var item in AutoProperties)
            {
                properties[item.Key] =  item.Value;
            }
            // These auto properties can change in runtime so don't bake them
            properties["$screen_width"] = Screen.width;
            properties["$screen_height"] = Screen.height;
            foreach (var item in OnceProperties)
            {
                properties[item.Key] =  item.Value;
            }
            OnceProperties = new Value();
            foreach (var item in SuperProperties)
            {
                properties[item.Key] = item.Value;
            }
            object startTime;
            if (TimedEvents.TryGetValue(eventName, out startTime))
            {
                properties["$duration"] = CurrentTime() - (double)startTime;
                var events = TimedEvents;
                events.Remove(eventName);
                TimedEvents = events;
            }
            properties["token"] = MixpanelSettings.Instance.Token;
            properties["distinct_id"] = DistinctID;
            properties["time"] = CurrentTime();
            var data = new Value() { {"event", eventName}, {"properties", properties} };
            DoRequest("https://api.mixpanel.com/track", data);
            // "https://api.mixpanel.com/import" if time is long?!?
        }

        internal static Value collectAutoProperties()
        {
            Value properties = new Value();
            properties["$app_build_number"] = Application.version;
            properties["$app_version"] = Application.unityVersion;
            properties["$device"] = Application.platform.ToString();
            properties["$model"] = SystemInfo.deviceModel;
            properties["$os"] = SystemInfo.operatingSystemFamily.ToString();
            properties["$os_version"] = SystemInfo.operatingSystem;
            properties["$screen_dpi"] = Screen.dpi;
            properties["$wifi"] = Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork;
            properties["mp_lib"] = "unity";
            return properties;
        }

        internal static void engage(Value properties)
        {
            if (!IsTracking) return;
            properties["$token"] = MixpanelSettings.Instance.Token;
            properties["$distinct_id"] = DistinctID;
            DoRequest("https://api.mixpanel.com/engage", properties);
        }

        internal static void DoRequest(string endpoint, Value data)
        {
            var request = UnityWebRequest.Get(BuildURL(endpoint, data));
            #if UNITY_EDITOR
            if (EditorApplication.isPlaying)
                RuntimeRequest(request);
            else
                EditorRequest(request);
            #else
                RuntimeRequest(request);
            #endif
        }

        internal static void EditorRequest(UnityWebRequest request)
        {
            IEnumerator e = HandleRequest(request);
            while (e.MoveNext());
        }

        internal static void RuntimeRequest(UnityWebRequest request)
        {
            if (async == null) async = new GameObject("Mixpanel").AddComponent<MixpanelAsync>();
            async.Enqueue(HandleRequest(request));
        }

        internal static IEnumerator HandleRequest(UnityWebRequest request)
        {
            yield return request.SendWebRequest();
            while (!request.isDone) yield return new WaitForEndOfFrame();
            if (MixpanelSettings.Instance.ShowDebug)
            {
                if (request.isNetworkError || request.isHttpError)
                    Debug.Log(request.error);
                else
                    Debug.Log(request.downloadHandler.text);
            }
        }

        internal static double CurrentTime()
        {
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            double currentEpochTime = (double)(DateTime.UtcNow - epochStart).TotalSeconds;
            return currentEpochTime;
        }

        internal static string CurrentDateTime()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        }
    }
}
