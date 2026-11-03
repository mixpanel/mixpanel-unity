using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace mixpanel
{
    /// <summary>
    /// PlayMode tests for the flush guard (SDK-440): a flush that fails in any way must not
    /// block later flushes, a pending request must never be sent a second time, and a flush
    /// interrupted by Clear or Disable must never delete newer events.
    /// Network is a local HTTP server; storage is an in-memory IPreferences.
    /// </summary>
    public class MixpanelFlushTests
    {
        private const float WaitSeconds = 10f;

        private FlushTestServer _server;
        private FaultyPreferences _prefs;
        private MonoBehaviour _controller;
        private string _savedTrackUrl;
        private string _savedEngageUrl;
        private readonly List<bool> _results = new List<bool>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Stop the auto-initialized SDK so its 60s flush loop can't interfere.
            MonoBehaviour existing = SdkInternals.Instance;
            if (existing != null)
            {
                existing.StopAllCoroutines();
                Object.Destroy(existing.gameObject);
                yield return null;
            }

            _prefs = new FaultyPreferences();
            MixpanelStorage.SetPreferencesSource(_prefs);
            SdkInternals.ResetRetryBackoff();

            _server = new FlushTestServer();
            _savedTrackUrl = SdkInternals.TrackUrl;
            _savedEngageUrl = SdkInternals.EngageUrl;
            SdkInternals.TrackUrl = _server.BaseUrl + "track/?ip=1";
            SdkInternals.EngageUrl = _server.BaseUrl + "engage/?ip=1";

            _controller = SdkInternals.CreateControllerWithoutFlushLoop();
            _results.Clear();
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
            _server.Dispose();
            SdkInternals.TrackUrl = _savedTrackUrl;
            SdkInternals.EngageUrl = _savedEngageUrl;
            SdkInternals.ResetRetryBackoff();
            MixpanelStorage.SetPreferencesSource(new PlayerPreferences());
        }

        // 1
        [UnityTest]
        public IEnumerator NormalFlush_DeliversEverythingAndReleasesGuard()
        {
            SdkInternals.EnqueueEvent("E1");
            SdkInternals.EnqueueEvent("E2");
            SdkInternals.EnqueuePeople("P1");

            yield return FlushAndWait();

            CollectionAssert.AreEqual(new[] { true }, _results);
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");
            Assert.IsTrue(_server.Delivered("E1") && _server.Delivered("E2") && _server.Delivered("P1"), "all events delivered");
            CollectionAssert.IsEmpty(_prefs.StoredPayloads, "storage drained");
        }

        // 2
        [UnityTest]
        public IEnumerator StorageThrowsWhileReadingQueue_ReleasesGuardImmediately()
        {
            SdkInternals.EnqueueEvent("E1");
            _prefs.Fault = (operation, key) => operation == "HasKey" && key.StartsWith("Event");
            LogAssert.Expect(LogType.Error, new Regex(@"\[Mixpanel\] Flush failed: .*Simulated storage failure: HasKey"));

            yield return FlushAndWait();

            CollectionAssert.AreEqual(new[] { false }, _results);
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");

            _prefs.Fault = (operation, key) => false;
            yield return FlushAndWait();

            Assert.IsTrue(_results.Last(), "next flush succeeds");
            Assert.IsTrue(_server.Delivered("E1"), "E1 delivered by the next flush");
        }

        // 3
        [UnityTest]
        public IEnumerator StorageThrowsWhileDeletingSentEvents_ReleasesGuardImmediately()
        {
            SdkInternals.EnqueueEvent("E1");
            _prefs.Fault = (operation, key) => operation == "DeleteKey" && key.StartsWith("Event");
            LogAssert.Expect(LogType.Error, new Regex(@"\[Mixpanel\] Flush failed: .*Simulated storage failure: DeleteKey"));

            yield return FlushAndWait();

            CollectionAssert.AreEqual(new[] { false }, _results);
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");

            _prefs.Fault = (operation, key) => false;
            yield return FlushAndWait();

            Assert.IsTrue(_results.Last(), "next flush succeeds");
            CollectionAssert.IsEmpty(_prefs.StoredPayloads, "storage drained");
        }

        // 4
        [UnityTest]
        public IEnumerator ServerError_KeepsEventsForRetryAndReleasesGuard()
        {
            SdkInternals.EnqueueEvent("E1");
            _server.NextReply = FlushTestServer.Reply.Error;

            yield return FlushAndWait();

            CollectionAssert.AreEqual(new[] { false }, _results);
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");
            Assert.IsTrue(_prefs.HasStored("E1"), "event kept for retry");
        }

        // 5
        [UnityTest]
        public IEnumerator UserCallbackThrows_ReleasesGuardAndNextFlushWorks()
        {
            SdkInternals.EnqueueEvent("E1");
            int calls = 0;
            LogAssert.Expect(LogType.Error, new Regex(@"\[Mixpanel\] onFlushComplete callback threw: .*callback failure"));

            SdkInternals.Flush(_controller, ok => { calls++; throw new InvalidOperationException("callback failure"); });
            yield return WaitFor(() => calls > 0, WaitSeconds, "the throwing callback");
            yield return null;

            Assert.AreEqual(1, calls, "callback invoked exactly once");
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");

            SdkInternals.EnqueueEvent("E2");
            yield return FlushAndWait();

            CollectionAssert.AreEqual(new[] { true }, _results);
            Assert.IsTrue(_server.Delivered("E2"), "next flush delivers E2");
        }

        // 6 — waits out the real 60s request timeout.
        [UnityTest, Timeout(150000)]
        public IEnumerator HungRequest_TimesOutAndReleasesGuard()
        {
            SdkInternals.EnqueueEvent("E1");
            _server.NextReply = FlushTestServer.Reply.Hold;
            float started = Time.realtimeSinceStartup;

            Flush();
            yield return WaitFor(() => _server.HeldCount == 1, WaitSeconds, "the request to reach the server");
            Assert.IsTrue(SdkInternals.IsFlushing, "guard held while the request is in flight");

            yield return WaitFor(() => _results.Count == 1, 90f, "the request timeout");
            float elapsed = Time.realtimeSinceStartup - started;

            CollectionAssert.AreEqual(new[] { false }, _results);
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");
            Assert.That(elapsed, Is.InRange(55f, 80f), "failed by the 60s request timeout");
            Assert.IsTrue(_prefs.HasStored("E1"), "event kept for retry");
        }

        // 7
        [UnityTest]
        public IEnumerator CoroutinesStoppedMidSend_ResponseStillProcessedWithoutResend()
        {
            SdkInternals.EnqueueEvent("E1");
            _server.NextReply = FlushTestServer.Reply.Hold;
            bool? firstResult = null;
            SdkInternals.Flush(_controller, ok => firstResult = ok);
            yield return WaitFor(() => _server.HeldCount == 1, WaitSeconds, "the request to reach the server");

            _controller.StopAllCoroutines();
            Flush();

            CollectionAssert.AreEqual(new[] { false }, _results, "a flush while one is in flight is rejected");
            Assert.IsTrue(SdkInternals.IsFlushing, "guard held until the request completes");

            _server.ReleaseOldest(true);
            yield return WaitFor(() => firstResult.HasValue, WaitSeconds, "the first flush to finish");

            Assert.AreEqual(true, firstResult, "first flush succeeds although its coroutines were stopped");
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");
            Assert.AreEqual(1, _server.RequestCount, "E1 sent once");
            CollectionAssert.IsEmpty(_prefs.StoredPayloads, "storage drained");
        }

        // 8 — after a long background stint, realtimeSinceStartup makes a pending request look old.
        [UnityTest]
        public IEnumerator FlushRightAfterResume_LeavesPendingRequestAlone()
        {
            SdkInternals.EnqueueEvent("E1");
            _server.NextReply = FlushTestServer.Reply.Hold;
            bool? firstResult = null;
            SdkInternals.Flush(_controller, ok => firstResult = ok);
            yield return WaitFor(() => _server.HeldCount == 1, WaitSeconds, "the request to reach the server");

            SdkInternals.BackdateInFlight(0, sentSecondsAgo: 1000f);
            Flush(); // first check only marks the request as possibly hung
            Flush(); // second check comes too soon after the first to abort

            CollectionAssert.AreEqual(new[] { false, false }, _results, "both flushes rejected");
            Assert.AreEqual(1, _server.RequestCount, "nothing resent");

            _server.ReleaseOldest(true);
            yield return WaitFor(() => firstResult.HasValue, WaitSeconds, "the first flush to finish");

            Assert.AreEqual(true, firstResult, "pending request completes normally");
            Assert.AreEqual(1, _server.DeliveredCount("E1"), "E1 delivered exactly once");
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");
            LogAssert.NoUnexpectedReceived();
        }

        // 9 — simulates a platform that ignores request.timeout.
        [UnityTest]
        public IEnumerator RequestHungPastTimeout_IsAbortedAndEventsKeptForRetry()
        {
            SdkInternals.EnqueueEvent("E1");
            _server.NextReply = FlushTestServer.Reply.Hold;
            bool? firstResult = null;
            SdkInternals.Flush(_controller, ok => firstResult = ok);
            yield return WaitFor(() => _server.HeldCount == 1, WaitSeconds, "the request to reach the server");

            SdkInternals.BackdateInFlight(0, sentSecondsAgo: 1000f);
            Flush(); // marks the request as possibly hung
            SdkInternals.BackdateInFlight(0, sentSecondsAgo: null, hungSecondsAgo: 1000f);
            LogAssert.Expect(LogType.Error, new Regex(@"\[Mixpanel\] Flush request did not complete; aborting it"));
            Flush(); // still pending on the second check: aborted

            yield return WaitFor(() => firstResult.HasValue, WaitSeconds, "the aborted flush to finish");

            Assert.AreEqual(false, firstResult, "aborted flush reports failure");
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");
            Assert.IsTrue(_prefs.HasStored("E1"), "E1 kept for retry");
            Assert.AreEqual(1, _server.RequestCount, "nothing resent while the request was pending");

            SdkInternals.ResetRetryBackoff();
            _server.NextReply = FlushTestServer.Reply.Ok;
            yield return FlushAndWait();

            Assert.IsTrue(_results.Last(), "retry succeeds");
            Assert.IsTrue(_server.Delivered("E1"), "E1 delivered by the retry");
        }

        // 10
        [UnityTest]
        public IEnumerator ClearDuringFlush_DoesNotDeleteNewerEventThatReusedItsKey()
        {
            SdkInternals.EnqueueEvent("OLD"); // stored as Event0
            _server.NextReply = FlushTestServer.Reply.Hold;
            bool? firstResult = null;
            SdkInternals.Flush(_controller, ok => firstResult = ok);
            yield return WaitFor(() => _server.HeldCount == 1, WaitSeconds, "the request to reach the server");

            SdkInternals.ClearQueues();       // drains the queue and resets ids
            SdkInternals.EnqueueEvent("NEW"); // reuses key Event0
            _server.ReleaseOldest(true);
            yield return WaitFor(() => firstResult.HasValue, WaitSeconds, "the first flush to finish");

            Assert.AreEqual(false, firstResult, "flush interrupted by Clear reports failure");
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");
            Assert.IsTrue(_prefs.HasStored("NEW"), "NEW not deleted by the earlier flush");

            _server.NextReply = FlushTestServer.Reply.Ok;
            yield return FlushAndWait();

            Assert.IsTrue(_server.Delivered("NEW"), "NEW delivered by the next flush");
        }

        // 11
        [UnityTest]
        public IEnumerator DisableMidFlush_ReleasesGuardAndNextSessionSendsEvents()
        {
            SdkInternals.EnqueueEvent("E1");
            _server.NextReply = FlushTestServer.Reply.Hold;
            bool? firstResult = null;
            SdkInternals.Flush(_controller, ok => firstResult = ok);
            yield return WaitFor(() => _server.HeldCount == 1, WaitSeconds, "the request to reach the server");

            GameObject oldHost = _controller.gameObject;
            SdkInternals.Disable();
            Object.Destroy(oldHost);

            Assert.IsFalse(SdkInternals.IsFlushing, "guard released by Disable");
            yield return WaitFor(() => firstResult.HasValue, WaitSeconds, "the aborted flush to finish");
            Assert.AreEqual(false, firstResult, "flush interrupted by Disable reports failure");
            Assert.IsTrue(_prefs.HasStored("E1"), "E1 kept for the next session");

            _controller = SdkInternals.CreateControllerWithoutFlushLoop();
            yield return null;
            _server.NextReply = FlushTestServer.Reply.Ok;
            yield return FlushAndWait();

            Assert.IsTrue(_results.Last(), "flush in the next session succeeds");
            Assert.IsTrue(_server.Delivered("E1"), "E1 delivered");
        }

        // 12 — last resort: Abort() never finishing the request is not something Unity has shown.
        [UnityTest]
        public IEnumerator AbortedRequestThatNeverCompletes_GuardIsReleased()
        {
            SdkInternals.EnqueueEvent("E1");
            var neverSent = new UnityEngine.Networking.UnityWebRequest(_server.BaseUrl);
            SdkInternals.InjectAbortedRequestThatNeverCompletes(0, neverSent);
            LogAssert.Expect(LogType.Error, new Regex(@"\[Mixpanel\] An aborted flush request never completed; releasing the flush guard"));

            yield return FlushAndWait();

            CollectionAssert.AreEqual(new[] { true }, _results, "new flush runs and succeeds");
            Assert.IsTrue(_server.Delivered("E1"), "E1 delivered");
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");
            neverSent.Dispose();
        }

        // 13 — the exception is thrown synchronously, inside DoFlush itself.
        [UnityTest]
        public IEnumerator StorageThrowsOnFirstStepOfBothQueues_CallbackFiresOnceAndGuardReleased()
        {
            SdkInternals.EnqueueEvent("E1");
            SdkInternals.EnqueuePeople("P1");
            _prefs.Fault = (operation, key) => operation == "HasKey" && FaultyPreferences.IsTrackingKey(key);
            LogAssert.Expect(LogType.Error, new Regex(@"\[Mixpanel\] Flush failed: .*HasKey\(Event"));
            LogAssert.Expect(LogType.Error, new Regex(@"\[Mixpanel\] Flush failed: .*HasKey\(People"));

            Flush();
            yield return null;

            CollectionAssert.AreEqual(new[] { false }, _results, "callback fired exactly once");
            Assert.IsFalse(SdkInternals.IsFlushing, "guard released");

            _prefs.Fault = (operation, key) => false;
            yield return FlushAndWait();

            Assert.IsTrue(_results.Last(), "next flush succeeds");
            Assert.IsTrue(_server.Delivered("E1") && _server.Delivered("P1"), "both queues delivered");
        }

        private void Flush() => SdkInternals.Flush(_controller, ok => _results.Add(ok));

        private IEnumerator FlushAndWait()
        {
            int before = _results.Count;
            Flush();
            yield return WaitFor(() => _results.Count > before, WaitSeconds, "the flush to finish");
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds, string what)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }
    }
}
