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
        internal static MixpanelAsync Async;
        internal static Value OnceProperties = new Value();
        internal static Value AutoProperties;

        internal static string Base64Encode(string text) {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }

        internal static string BuildURL(string endpoint, Value data)
        {
            if (MixpanelSettings.Instance.ShowDebug) Debug.Log(Json.Serialize(data));
            return $"{endpoint}/?ip=1&data={Base64Encode(Json.Serialize(data))}";
        }

        internal static void DoTrack(string eventName, Value properties)
        {
            if (!IsTracking) return;
            if (AutoProperties == null) AutoProperties = CollectAutoProperties();
            foreach (KeyValuePair<string, object> item in AutoProperties)
            {
                properties[item.Key] =  item.Value;
            }
            // These auto properties can change in runtime so don't bake them
            properties["$screen_width"] = Screen.width;
            properties["$screen_height"] = Screen.height;
            foreach (KeyValuePair<string, object> item in OnceProperties)
            {
                properties[item.Key] =  item.Value;
            }
            OnceProperties = new Value();
            foreach (KeyValuePair<string, object> item in SuperProperties)
            {
                properties[item.Key] = item.Value;
            }

            if (TimedEvents.TryGetValue(eventName, out object startTime))
            {
                properties["$duration"] = CurrentTime() - (double)startTime;
                Value events = TimedEvents;
                events.Remove(eventName);
                TimedEvents = events;
            }
            properties["token"] = MixpanelSettings.Instance.Token;
            properties["distinct_id"] = DistinctID;
            properties["time"] = CurrentTime();
            Value data = new Value { {"event", eventName}, {"properties", properties} };
            DoRequest("https://api.mixpanel.com/track", data);
            // "https://api.mixpanel.com/import" if time is long?!?
        }

        internal static Value CollectAutoProperties()
        {
            Value properties = new Value
            {
                ["$app_build_number"] = Application.version,
                ["$app_version"] = Application.unityVersion,
                ["$device"] = Application.platform.ToString(),
                ["$model"] = SystemInfo.deviceModel,
                ["$os"] = SystemInfo.operatingSystemFamily.ToString(),
                ["$os_version"] = SystemInfo.operatingSystem,
                ["$screen_dpi"] = Screen.dpi,
                ["$wifi"] = Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork,
                ["mp_lib"] = "unity"
            };
            return properties;
        }

        internal static void DoEngage(Value properties)
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
            if (Async == null) Async = new GameObject("Mixpanel").AddComponent<MixpanelAsync>();
            Async.Enqueue(HandleRequest(request));
        }

        internal static IEnumerator HandleRequest(UnityWebRequest request)
        {
            yield return request.SendWebRequest();
            while (!request.isDone) yield return new WaitForEndOfFrame();
            if (!MixpanelSettings.Instance.ShowDebug) yield break;
            if (request.isNetworkError || request.isHttpError)
                Debug.Log(request.error);
            else
                Debug.Log(request.downloadHandler.text);
        }

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
    }
}
