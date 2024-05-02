using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using System.Net;
using System.Net.Http;
using System.Web;
using UnityEngine.Networking;
using Unity.Jobs;
using Unity.Collections;

namespace mixpanel
{
    public static class MixpanelStorage
    {
        #region Preferences
        private static IPreferences PreferencesSource = new PlayerPreferences();

        public static void SetPreferencesSource(IPreferences preferences)
        {
            PreferencesSource = preferences;
        }

        #endregion

        #region HasMigratedFrom1To2

        private const string HasMigratedFrom1To2Name = "Mixpanel.HasMigratedFrom1To2";

        internal static bool HasMigratedFrom1To2
        {
            get => Convert.ToBoolean(PreferencesSource.GetInt(HasMigratedFrom1To2Name, 0));
            set => PreferencesSource.SetInt(HasMigratedFrom1To2Name, Convert.ToInt32(value));
        }

        #endregion

        #region DistinctId

        private const string DistinctIdName = "Mixpanel.DistinctId";

        private static string _distinctId;

        public static string DistinctId
        {
            get
            {
                if (!string.IsNullOrEmpty(_distinctId)) return _distinctId;
                if (PreferencesSource.HasKey(DistinctIdName))
                {
                    _distinctId = PreferencesSource.GetString(DistinctIdName);
                }
                // Generate a Unique ID for this client if still null or empty
                // https://devblogs.microsoft.com/oldnewthing/?p=21823
                if (string.IsNullOrEmpty(_distinctId)) DistinctId = Guid.NewGuid().ToString();
                return _distinctId;
            }
            set
            {
                _distinctId = value;
                PreferencesSource.SetString(DistinctIdName, _distinctId);
            }
        }

        #endregion

        #region Track

        private const string EventAutoIncrementingIdName = "EventAutoIncrementingID";
        private const string PeopleAutoIncrementingIdName = "PeopleAutoIncrementingID";

        // For performance, we can store the lowest unsent event ID to prevent searching from 0.
        // This search process can be slow if the auto-increment ID gets large enough.
        private const string EventStartIndexName = "EventStartIndex";
        private const string PeopleStartIndexName = "PeopleStartIndex";

        internal enum FlushType
        {
            EVENTS,
            PEOPLE,
        }

        internal static void EnqueueTrackingData(Value data, FlushType flushType)
        {
            int eventId = EventAutoIncrementingID();
            int peopleId = PeopleAutoIncrementingID();
            String trackingKey = (flushType == FlushType.EVENTS)? "Event" + eventId.ToString() : "People" + peopleId.ToString();
            data["id"] = trackingKey;
            PreferencesSource.SetString(trackingKey, JsonUtility.ToJson(data));
            IncreaseTrackingDataID(flushType);
        }

        internal static int EventAutoIncrementingID()
        {
            return PreferencesSource.GetInt(EventAutoIncrementingIdName, 0);
        }

        internal static int PeopleAutoIncrementingID()
        {
            return PreferencesSource.GetInt(PeopleAutoIncrementingIdName, 0);
        }

        internal static int EventStartIndex()
        {
            return PreferencesSource.GetInt(EventStartIndexName, 0);
        }

        internal static int PeopleStartIndex()
        {
            return PreferencesSource.GetInt(PeopleStartIndexName, 0);
        }

        private static void IncreaseTrackingDataID(FlushType flushType)
        {
            int id = (flushType == FlushType.EVENTS)? EventAutoIncrementingID() : PeopleAutoIncrementingID();
            id += 1;
            String trackingIdKey = (flushType == FlushType.EVENTS)? EventAutoIncrementingIdName : PeopleAutoIncrementingIdName;
            PreferencesSource.SetInt(trackingIdKey, id);
        }

        internal static Value DequeueBatchTrackingData(FlushType flushType, int batchSize)
        {
            Value batch = Value.Array;
            string startIndexKey = (flushType == FlushType.EVENTS) ? EventStartIndexName : PeopleStartIndexName;
            int oldStartIndex = (flushType == FlushType.EVENTS) ? EventStartIndex() : PeopleStartIndex();
            int newStartIndex = oldStartIndex;
            int dataIndex = oldStartIndex;
            int maxIndex = (flushType == FlushType.EVENTS) ? EventAutoIncrementingID() - 1 : PeopleAutoIncrementingID() - 1;
            while (batch.Count < batchSize && dataIndex <= maxIndex) {
                String trackingKey = (flushType == FlushType.EVENTS) ? "Event" + dataIndex.ToString() : "People" + dataIndex.ToString();
                if (PreferencesSource.HasKey(trackingKey)) {
                    try {
                        batch.Add(JsonUtility.FromJson<Value>(PreferencesSource.GetString(trackingKey)));
                    }
                    catch (Exception e) {
                        Mixpanel.LogError($"There was an error processing '{trackingKey}' from the internal object pool: " + e);
                        PreferencesSource.DeleteKey(trackingKey);

                        if (batch.Count == 0) {
                            // Only update if we didn't find a key prior to deleting this key, since the prior key would be a lower valid index.
                            newStartIndex = Math.Min(dataIndex + 1, maxIndex);
                        }
                    }
                }
                else if (batch.Count == 0) {
                    // Keep updating the start index as long as we haven't found anything for our batch yet -- we're looking for the minimum index.
                    newStartIndex = Math.Min(dataIndex + 1, maxIndex);
                }
                dataIndex++;
            }

            if (newStartIndex != oldStartIndex) {
                PreferencesSource.SetInt(startIndexKey, newStartIndex);
            }

            return batch;
        }

        internal static void DeleteBatchTrackingData(FlushType flushType, int batchSize)
        {
            int deletedCount = 0;
            string startIndexKey = (flushType == FlushType.EVENTS) ? EventStartIndexName : PeopleStartIndexName;
            int oldStartIndex = (flushType == FlushType.EVENTS) ? EventStartIndex() : PeopleStartIndex();
            int newStartIndex = oldStartIndex;
            int dataIndex = oldStartIndex;
            int maxIndex = (flushType == FlushType.EVENTS) ? EventAutoIncrementingID() - 1 : PeopleAutoIncrementingID() - 1;
            while (deletedCount < batchSize && dataIndex <= maxIndex) {
                String trackingKey = (flushType == FlushType.EVENTS) ? "Event" + dataIndex.ToString() : "People" + dataIndex.ToString();
                if (PreferencesSource.HasKey(trackingKey)) {
                    PreferencesSource.DeleteKey(trackingKey);
                    deletedCount++;
                }
                newStartIndex = Math.Min(dataIndex + 1, maxIndex);
                dataIndex++;
            }

            if (dataIndex == maxIndex) {
                // We want to avoid maxIndex from getting too high while having large "empty gaps" stored in PlayerPrefs, otherwise
                // there can be a large number of string concatenation and PlayerPrefs API calls (in extreme cases, 100K+).
                // At this point, we should have iterated through all possible event IDs and can assume that there are no other events
                // stored in preferences (since we deleted them all).
                string idKey = (flushType == FlushType.EVENTS) ? EventAutoIncrementingIdName : PeopleAutoIncrementingIdName;
                PreferencesSource.SetInt(idKey, 0);
                PreferencesSource.SetInt(startIndexKey, 0);
            }
            else if (newStartIndex != oldStartIndex) {
                // There are unsent batches, store the index of where to resume searching for next time.
                PreferencesSource.SetInt(startIndexKey, newStartIndex);
            }
        }

        internal static void DeleteBatchTrackingData(Value batch) {
            foreach(Value data in batch) {
                String id = data["id"];
                if (id != null && PreferencesSource.HasKey(id)) {
                    PreferencesSource.DeleteKey(id);
                }
            }
        }

        internal static void DeleteAllTrackingData(FlushType flushType)
        {
            DeleteBatchTrackingData(flushType, int.MaxValue);
        }

        #endregion

        #region IsTracking

        private const string IsTrackingName = "Mixpanel.IsTracking";

        private static bool _isTracking;

        public static bool IsTracking
        {
            get
            {
                if (!PreferencesSource.HasKey(IsTrackingName)) IsTracking = true;
                else _isTracking = PreferencesSource.GetInt(IsTrackingName) == 1;
                return _isTracking;
            }
            set
            {
                _isTracking = value;
                PreferencesSource.SetInt(IsTrackingName, _isTracking ? 1 : 0);
            }
        }

        #endregion

        #region OnceProperties

        private const string OncePropertiesName = "Mixpanel.OnceProperties";

        private static Value _onceProperties;

        internal static Value OnceProperties
        {
            get
            {
                if (_onceProperties != null) return _onceProperties;
                if (!PreferencesSource.HasKey(OncePropertiesName)) OnceProperties = new Value();
                else
                {
                    _onceProperties = new Value();
                    JsonUtility.FromJsonOverwrite(PreferencesSource.GetString(OncePropertiesName), _onceProperties);
                }
                return _onceProperties;
            }
            set
            {
                _onceProperties = value;
                PreferencesSource.SetString(OncePropertiesName, JsonUtility.ToJson(_onceProperties));
            }
        }

        internal static void ResetOnceProperties()
        {
            Value properties = OnceProperties;
            properties.OnRecycle();
            OnceProperties = properties;
        }

        #endregion

        #region SuperProperties

        private const string SuperPropertiesName = "Mixpanel.SuperProperties";

        private static Value _superProperties;

        internal static Value SuperProperties
        {
            get
            {
                if (_superProperties != null) return _superProperties;
                if (!PreferencesSource.HasKey(SuperPropertiesName)) SuperProperties = new Value();
                else
                {
                    _superProperties = new Value();
                    JsonUtility.FromJsonOverwrite(PreferencesSource.GetString(SuperPropertiesName), _superProperties);
                }
                return _superProperties;
            }
            set
            {
                _superProperties = value;
                PreferencesSource.SetString(SuperPropertiesName, JsonUtility.ToJson(_superProperties));
            }
        }

        internal static void ResetSuperProperties()
        {
            Value properties = SuperProperties;
            properties.OnRecycle();
            SuperProperties = properties;
        }

        #endregion

        #region TimedEvents

        private const string TimedEventsName = "Mixpanel.TimedEvents";

        private static Value _timedEvents;

        internal static Value TimedEvents
        {
            get
            {
                if (_timedEvents != null) return _timedEvents;
                if (!PreferencesSource.HasKey(TimedEventsName)) TimedEvents = new Value();
                else
                {
                    _timedEvents = new Value();
                    JsonUtility.FromJsonOverwrite(PreferencesSource.GetString(TimedEventsName), _timedEvents);
                }
                return _timedEvents;
            }
            set
            {
                _timedEvents = value;
                PreferencesSource.SetString(TimedEventsName, JsonUtility.ToJson(_timedEvents));
            }
        }

        internal static void ResetTimedEvents()
        {
            Value properties = TimedEvents;
            properties.OnRecycle();
            TimedEvents = properties;
        }

        #endregion
    }
}
