using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace mixpanel
{
    /// <summary>
    /// Reflection access to the SDK internals the flush tests drive. The SDK has no
    /// InternalsVisibleTo, and these tests deliberately need no production changes.
    /// </summary>
    internal static class SdkInternals
    {
        private const BindingFlags StaticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly Assembly Sdk = typeof(Mixpanel).Assembly;
        private static readonly Type ControllerType = Sdk.GetType("mixpanel.Controller", true);
        private static readonly Type ConfigType = Sdk.GetType("mixpanel.Config", true);
        private static readonly Type FlushType = typeof(MixpanelStorage).GetNestedType("FlushType", BindingFlags.NonPublic);

        private static object GetStatic(Type type, string name) => Field(type, name).GetValue(null);
        private static void SetStatic(Type type, string name, object value) => Field(type, name).SetValue(null, value);

        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, StaticFlags);
            if (field == null) throw new MissingFieldException(type.Name, name);
            return field;
        }

        internal static MonoBehaviour Instance => (MonoBehaviour)GetStatic(ControllerType, "_instance");

        internal static bool IsFlushing => (bool)GetStatic(ControllerType, "_isFlushing");

        internal static string TrackUrl
        {
            get => (string)GetStatic(ConfigType, "TrackUrl");
            set => SetStatic(ConfigType, "TrackUrl", value);
        }

        internal static string EngageUrl
        {
            get => (string)GetStatic(ConfigType, "EngageUrl");
            set => SetStatic(ConfigType, "EngageUrl", value);
        }

        /// <summary>Creates the controller without letting Start() begin the 60s auto-flush loop.</summary>
        internal static MonoBehaviour CreateControllerWithoutFlushLoop()
        {
            SetStatic(ControllerType, "_fullyInitialized", true);
            SetStatic(ControllerType, "_isFlushCoroutineRunning", true);
            return (MonoBehaviour)ControllerType.GetMethod("GetInstance", StaticFlags).Invoke(null, null);
        }

        internal static void ResetRetryBackoff()
        {
            SetStatic(ControllerType, "_retryCount", 0);
            SetStatic(ControllerType, "_retryTime", DateTime.MinValue);
        }

        /// <summary>Makes the in-progress flush look like it has made no progress for a long time.</summary>
        internal static void BackdateFlushProgress()
        {
            SetStatic(ControllerType, "_flushLastProgress", Time.realtimeSinceStartup - 1000f);
        }

        internal static void Flush(MonoBehaviour controller, Action<bool> onFlushComplete)
        {
            try
            {
                ControllerType.GetMethod("DoFlush", InstanceFlags).Invoke(controller, new object[] { onFlushComplete });
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        internal static void EnqueueEvent(string name) => Enqueue(name, "EVENTS");

        internal static void EnqueuePeople(string name) => Enqueue(name, "PEOPLE");

        private static void Enqueue(string name, string flushType)
        {
            Value data = new Value();
            data["event"] = name;
            typeof(MixpanelStorage)
                .GetMethod("EnqueueTrackingData", StaticFlags)
                .Invoke(null, new[] { data, Enum.Parse(FlushType, flushType) });
        }
    }

    /// <summary>
    /// In-memory IPreferences that can be told to throw on chosen operations, standing in
    /// for WebGL storage overflow or a custom IPreferences that throws.
    /// </summary>
    internal sealed class FaultyPreferences : IPreferences
    {
        private static readonly Regex TrackingKey = new Regex(@"^(Event|People)\d+$");
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        /// <summary>Return true from (operation, key) to make that call throw.</summary>
        internal Func<string, string, bool> Fault = (operation, key) => false;

        internal static bool IsTrackingKey(string key) => TrackingKey.IsMatch(key);

        internal IEnumerable<string> StoredPayloads =>
            _values.Where(kv => IsTrackingKey(kv.Key)).Select(kv => (string)kv.Value);

        internal bool HasStored(string eventName) => StoredPayloads.Any(p => p.Contains("\"" + eventName + "\""));

        private void Check(string operation, string key)
        {
            if (Fault(operation, key)) throw new InvalidOperationException("Simulated storage failure: " + operation + "(" + key + ")");
        }

        public void DeleteKey(string key) { Check("DeleteKey", key); _values.Remove(key); }
        public int GetInt(string key) => GetInt(key, 0);
        public int GetInt(string key, int defaultValue) { Check("GetInt", key); return _values.TryGetValue(key, out object v) ? (int)v : defaultValue; }
        public string GetString(string key) => GetString(key, "");
        public string GetString(string key, string defaultValue) { Check("GetString", key); return _values.TryGetValue(key, out object v) ? (string)v : defaultValue; }
        public bool HasKey(string key) { Check("HasKey", key); return _values.ContainsKey(key); }
        public void SetInt(string key, int value) { Check("SetInt", key); _values[key] = value; }
        public void SetString(string key, string value) { Check("SetString", key); _values[key] = value; }
    }

    /// <summary>
    /// Local stand-in for the Mixpanel ingestion endpoints. Each incoming request is answered
    /// according to <see cref="NextReply"/>: 200, 503, or held open until the test releases it.
    /// </summary>
    internal sealed class FlushTestServer : IDisposable
    {
        internal enum Reply { Ok, Error, Hold }

        private readonly HttpListener _listener = new HttpListener();
        private readonly object _gate = new object();
        private readonly List<string> _delivered = new List<string>();
        private readonly List<KeyValuePair<HttpListenerContext, string>> _held = new List<KeyValuePair<HttpListenerContext, string>>();
        private Reply _nextReply = Reply.Ok;
        private int _requestCount;

        internal string BaseUrl { get; }

        internal FlushTestServer()
        {
            BaseUrl = "http://127.0.0.1:" + FreePort() + "/";
            _listener.Prefixes.Add(BaseUrl);
            _listener.Start();
            _listener.BeginGetContext(OnRequest, null);
        }

        internal Reply NextReply
        {
            get { lock (_gate) return _nextReply; }
            set { lock (_gate) _nextReply = value; }
        }

        internal int RequestCount { get { lock (_gate) return _requestCount; } }

        internal int HeldCount { get { lock (_gate) return _held.Count; } }

        /// <summary>True once a batch containing this event has been answered with 200.</summary>
        internal bool Delivered(string eventName) => DeliveredCount(eventName) > 0;

        internal int DeliveredCount(string eventName)
        {
            lock (_gate) return _delivered.Count(p => p.Contains("\"" + eventName + "\""));
        }

        /// <summary>Answers the oldest held request.</summary>
        internal void ReleaseOldest(bool success)
        {
            KeyValuePair<HttpListenerContext, string> request;
            lock (_gate)
            {
                if (_held.Count == 0) return;
                request = _held[0];
                _held.RemoveAt(0);
            }
            Respond(request.Key, request.Value, success);
        }

        internal void ReleaseAll(bool success)
        {
            while (HeldCount > 0) ReleaseOldest(success);
        }

        private void OnRequest(IAsyncResult result)
        {
            HttpListenerContext context;
            try
            {
                context = _listener.EndGetContext(result);
                _listener.BeginGetContext(OnRequest, null);
            }
            catch (Exception)
            {
                return; // listener stopped
            }

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }
            string payload = body.StartsWith("data=") ? WebUtility.UrlDecode(body.Substring("data=".Length)) : body;

            Reply reply;
            lock (_gate)
            {
                _requestCount++;
                reply = _nextReply;
                if (reply == Reply.Hold)
                {
                    _held.Add(new KeyValuePair<HttpListenerContext, string>(context, payload));
                    return;
                }
            }
            Respond(context, payload, reply == Reply.Ok);
        }

        private void Respond(HttpListenerContext context, string payload, bool success)
        {
            if (success)
            {
                lock (_gate) _delivered.Add(payload);
            }
            try
            {
                context.Response.StatusCode = success ? 200 : 503;
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(success ? "1" : "0");
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.Close();
            }
            catch (Exception)
            {
                // The client may already have given up on this request (timeout, stopped coroutine).
            }
        }

        public void Dispose()
        {
            ReleaseAll(false);
            try { _listener.Stop(); _listener.Close(); } catch (Exception) { }
        }

        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
