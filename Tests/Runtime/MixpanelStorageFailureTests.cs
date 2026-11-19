using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace mixpanel
{
    /// <summary>
    /// PlayMode tests for storage write failures (SDK-347): on WebGL, PlayerPrefs throws once its
    /// ~1 MB quota is full. Public API calls must not throw into game code, the values set this
    /// session must stay in memory, and the event queue must stay consistent.
    /// </summary>
    public class MixpanelStorageFailureTests
    {
        private const string DistinctIdKey = "Mixpanel.DistinctId";
        private const string IsTrackingKey = "Mixpanel.IsTracking";
        private const string EventIdKey = "EventAutoIncrementingID";

        private FaultyPreferences _prefs;
        private MonoBehaviour _controller;
        private string _savedTrackUrl;
        private string _savedEngageUrl;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            MonoBehaviour existing = SdkInternals.Instance;
            if (existing != null)
            {
                existing.StopAllCoroutines();
                Object.Destroy(existing.gameObject);
                yield return null;
            }

            _prefs = new FaultyPreferences();
            MixpanelStorage.SetPreferencesSource(_prefs);
            ForgetCachedStorage();

            // Reset() calls Flush(); its queues are empty then, but never let a test reach the real API.
            _savedTrackUrl = SdkInternals.TrackUrl;
            _savedEngageUrl = SdkInternals.EngageUrl;
            SdkInternals.TrackUrl = "http://127.0.0.1:9/track/?ip=1";
            SdkInternals.EngageUrl = "http://127.0.0.1:9/engage/?ip=1";

            _controller = SdkInternals.CreateControllerWithoutFlushLoop();
            yield return null; // let Start() run
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_controller != null)
            {
                _controller.StopAllCoroutines();
                Object.Destroy(_controller.gameObject);
                yield return null;
            }
            SdkInternals.TrackUrl = _savedTrackUrl;
            SdkInternals.EngageUrl = _savedEngageUrl;
            MixpanelStorage.SetPreferencesSource(new PlayerPreferences());
            ForgetCachedStorage(); // so this test's identity and properties don't leak into real PlayerPrefs
        }

        // 1 — the WebGL case from the issue: the queue write is what overflows.
        [UnityTest]
        public IEnumerator Track_WhenQueueWriteFails_DoesNotThrowAndNextTrackIsQueued()
        {
            Mixpanel.Identify("user-1");
            Mixpanel.Register("plan", "pro");
            int idBefore = _prefs.GetInt(EventIdKey);

            _prefs.Fault = (operation, key) => operation == "SetString" && FaultyPreferences.IsTrackingKey(key);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Mixpanel\] Couldn't save Event\d+: .*Simulated storage failure"));
            Assert.DoesNotThrow(() => Mixpanel.Track("Dropped"));

            Assert.IsFalse(_prefs.HasStored("Dropped"), "nothing stored");
            Assert.AreEqual(idBefore, _prefs.GetInt(EventIdKey), "ID not advanced past an empty slot");

            _prefs.Fault = (operation, key) => false;
            Mixpanel.Track("Kept");

            Assert.AreEqual(idBefore + 1, _prefs.GetInt(EventIdKey));
            string kept = _prefs.StoredPayloads.Single(p => p.Contains("\"Kept\""));
            StringAssert.Contains("\"user-1\"", kept, "distinct_id still attached");
            StringAssert.Contains("\"pro\"", kept, "super property still attached");
            yield return null;
        }

        // 2 — every identity/property write fails; memory must still drive the events.
        [UnityTest]
        public IEnumerator IdentityAndPropertyWrites_WhenStorageFails_DoNotThrowAndStayInMemory()
        {
            _prefs.Fault = (operation, key) =>
                (operation == "SetString" || operation == "SetInt") && !FaultyPreferences.IsTrackingKey(key) && key != EventIdKey;

            Assert.DoesNotThrow(() =>
            {
                Mixpanel.Identify("user-2");
                Mixpanel.Register("plan", "free");
                Mixpanel.RegisterOnce("first_seen", "today");
                Mixpanel.StartTimedEvent("Level");
            });

            Assert.AreEqual("user-2", Mixpanel.DistinctId);
            Assert.AreEqual("free", (string)MixpanelStorage.SuperProperties["plan"]);
            Assert.AreEqual("today", (string)MixpanelStorage.OnceProperties["first_seen"]);
            Assert.IsTrue(MixpanelStorage.TimedEvents.ContainsKey("Level"));
            Assert.IsFalse(_prefs.HasKey("Mixpanel.SuperProperties"), "the write really failed");

            Assert.DoesNotThrow(() => Mixpanel.Track("Level")); // also rewrites TimedEvents, which fails

            string level = _prefs.StoredPayloads.Single(p => p.Contains("\"Level\""));
            StringAssert.Contains("\"user-2\"", level);
            StringAssert.Contains("\"free\"", level);
            StringAssert.Contains("\"today\"", level);
            StringAssert.Contains("\"$duration\"", level);
            Assert.IsFalse(MixpanelStorage.TimedEvents.ContainsKey("Level"), "timer consumed in memory");
            yield return null;
        }

        // 3
        [UnityTest]
        public IEnumerator PeopleSet_WhenQueueWriteFails_DoesNotThrow()
        {
            _prefs.Fault = (operation, key) => operation == "SetString" && FaultyPreferences.IsTrackingKey(key);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Mixpanel\] Couldn't save People\d+"));

            Assert.DoesNotThrow(() => Mixpanel.People.Set("plan", "pro"));

            Assert.IsEmpty(_prefs.StoredPayloads.ToList());
            Assert.AreEqual(0, _prefs.GetInt("PeopleAutoIncrementingID"));
            yield return null;
        }

        // 4 — the payload is written but the ID is not: the payload must not be silently
        // overwritten by the next Track, so it is removed and dropped instead.
        [UnityTest]
        public IEnumerator Track_WhenOnlyIdWriteFails_RemovesPayloadSoNextTrackDoesNotOverwriteIt()
        {
            Mixpanel.Identify("user-3");
            int idBefore = _prefs.GetInt(EventIdKey);

            _prefs.Fault = (operation, key) => operation == "SetInt" && key == EventIdKey;
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Mixpanel\] Couldn't save EventAutoIncrementingID"));
            Assert.DoesNotThrow(() => Mixpanel.Track("A"));

            Assert.IsFalse(_prefs.HasStored("A"), "orphaned payload removed");
            Assert.AreEqual(idBefore, _prefs.GetInt(EventIdKey));

            _prefs.Fault = (operation, key) => false;
            Mixpanel.Track("B");
            Mixpanel.Track("C");

            Assert.AreEqual(idBefore + 2, _prefs.GetInt(EventIdKey));
            Assert.IsTrue(_prefs.HasStored("B") && _prefs.HasStored("C"), "both queued in their own slots");
            yield return null;
        }

        // 5 — IsTracking is re-read from storage on every get; a failed opt-out must still hold.
        [UnityTest]
        public IEnumerator OptOut_WhenWriteFails_StaysOptedOutForTheSession()
        {
            MixpanelStorage.IsTracking = true;
            Assert.AreEqual(1, _prefs.GetInt(IsTrackingKey));

            _prefs.Fault = (operation, key) => operation == "SetInt" && key == IsTrackingKey;
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Mixpanel\] Couldn't save Mixpanel\.IsTracking"));
            Assert.DoesNotThrow(() => MixpanelStorage.IsTracking = false);

            Assert.AreEqual(1, _prefs.GetInt(IsTrackingKey), "the write really failed");
            Assert.IsFalse(MixpanelStorage.IsTracking, "memory wins over the stale stored value");
            Mixpanel.Track("WhileOptedOut");
            Assert.IsFalse(_prefs.HasStored("WhileOptedOut"));

            _prefs.Fault = (operation, key) => false;
            MixpanelStorage.IsTracking = true;
            Assert.IsTrue(MixpanelStorage.IsTracking);
            Assert.AreEqual(1, _prefs.GetInt(IsTrackingKey));
            yield return null;
        }

        // 6 — Reset sets distinct_id to "" so a new one is generated; if that write fails the
        // old stored ID must not come back.
        [UnityTest]
        public IEnumerator Reset_WhenDistinctIdWriteFails_DoesNotRestoreOldId()
        {
            Mixpanel.Identify("old-user");
            Assert.AreEqual("old-user", _prefs.GetString(DistinctIdKey));

            _prefs.Fault = (operation, key) => operation == "SetString" && key == DistinctIdKey;
            Assert.DoesNotThrow(() => Mixpanel.Reset());

            string newId = Mixpanel.DistinctId;
            Assert.IsFalse(string.IsNullOrEmpty(newId));
            Assert.AreNotEqual("old-user", newId);
            Assert.AreEqual(newId, Mixpanel.DistinctId, "stable for the session");

            _prefs.Fault = (operation, key) => false;
            Mixpanel.Identify("next-user");
            Assert.AreEqual("next-user", _prefs.GetString(DistinctIdKey), "saved once storage works again");
            yield return null;
        }

        /// <summary>
        /// MixpanelStorage caches identity and properties in static fields. Clear them so each
        /// test reads its own IPreferences, and nothing written here reaches real PlayerPrefs.
        /// </summary>
        private static void ForgetCachedStorage()
        {
            foreach (string name in new[] { "_distinctId", "_superProperties", "_onceProperties", "_timedEvents" })
                StorageField(name).SetValue(null, null);
            foreach (string name in new[] { "_distinctIdUnsaved", "_isTrackingUnsaved" })
                StorageField(name).SetValue(null, false);
        }

        private static FieldInfo StorageField(string name)
        {
            FieldInfo field = typeof(MixpanelStorage).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null) throw new System.MissingFieldException(nameof(MixpanelStorage), name);
            return field;
        }
    }
}
