using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace mixpanel
{
    public static partial class Mixpanel
    {
        internal static MixpanelAsync async;
        internal static Value OnceProperties = new Value();

        internal static string Base64Encode(string text) {
            var bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }

        internal static string BuildURL(string endpoint, Value data)
        {
            if (MixpanelSettings.Instance.ShowDebug) Debug.Log(JsonConvert.SerializeObject(data));
            return string.Format("{0}/?data={1}", endpoint, Base64Encode(JsonConvert.SerializeObject(data)));
        }

        internal static void track(string eventName, Value properties)
        {
            if (!IsTracking) return;
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
