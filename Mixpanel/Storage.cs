using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace mixpanel
{
    public static class MixpanelStorage
    {
        internal sealed class StoredBatch
        {
            internal readonly List<string> TrackingKeys;
            internal readonly string Payload;

            internal int Count => TrackingKeys.Count;

            internal StoredBatch(List<string> trackingKeys, string payload)
            {
                TrackingKeys = trackingKeys;
                Payload = payload;
            }
        }

        private static bool IsLegacySerializedValue(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;

            const string legacyPrefix = "{\"_valueType\":";
            int startIndex = 0;
            while (startIndex < json.Length && char.IsWhiteSpace(json[startIndex]))
            {
                startIndex++;
            }

            if (startIndex > json.Length - legacyPrefix.Length) return false;
            bool hasLegacyPrefix = string.Compare(
                json,
                startIndex,
                legacyPrefix,
                0,
                legacyPrefix.Length,
                StringComparison.Ordinal) == 0;
            if (!hasLegacyPrefix) return false;

            int valueTypeIndex = startIndex + legacyPrefix.Length;
            if (valueTypeIndex >= json.Length) return false;

            // JsonUtility emits ValueTypes as an integer, but a user payload may
            // also legitimately start with a top-level "_valueType" numeric
            // property. Require additional JsonUtility backing fields before
            // treating stored data as legacy format.
            char valueType = json[valueTypeIndex];
            return valueType >= '0' && valueType <= '9'
                && HasJsonUtilityField(json, startIndex, "\"_dataType\":")
                && (HasJsonUtilityField(json, startIndex, "\"_arrayData\":")
                    || (HasJsonUtilityField(json, startIndex, "\"_containerKeys\":")
                        && HasJsonUtilityField(json, startIndex, "\"_containerValues\":")));
        }

        private static bool HasJsonUtilityField(string json, int startIndex, string field)
        {
            return json.IndexOf(field, startIndex, StringComparison.Ordinal) >= 0;
        }

        private static void EnsureStoredPayloadIsObjectJson(string json)
        {
            int startIndex = 0;
            while (startIndex < json.Length && char.IsWhiteSpace(json[startIndex]))
            {
                startIndex++;
            }

            int endIndex = json.Length - 1;
            while (endIndex >= startIndex && char.IsWhiteSpace(json[endIndex]))
            {
                endIndex--;
            }

            if (startIndex > endIndex || json[startIndex] != '{' || json[endIndex] != '}')
                throw new FormatException("Stored tracking payload is not a JSON object.");
        }

        // Deserializes stored Value data, accepting both JsonUtility-backed data and
        // the newer Value.ToString() format during migration.
        private static Value DeserializeStored(string json)
        {
            if (string.IsNullOrEmpty(json)) return new Value();
            if (IsLegacySerializedValue(json))
                return JsonUtility.FromJson<Value>(json);
            return Value.Deserialize(json);
        }

        private static string SerializeStored(Value value)
        {
            return value == null ? "null" : value.ToString();
        }

        private static string NormalizeStoredPayload(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("Stored tracking payload is empty.");

            string normalizedPayload = IsLegacySerializedValue(json)
                ? JsonUtility.FromJson<Value>(json).ToString()
                : json;
            EnsureStoredPayloadIsObjectJson(normalizedPayload);
            return normalizedPayload;
        }
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

        private static string TrackingKey(FlushType flushType, int dataIndex)
        {
            return (flushType == FlushType.EVENTS) ? "Event" + dataIndex.ToString() : "People" + dataIndex.ToString();
        }

        private static string TrackingIdKey(FlushType flushType)
        {
            return (flushType == FlushType.EVENTS) ? EventAutoIncrementingIdName : PeopleAutoIncrementingIdName;
        }

        private static string StartIndexKey(FlushType flushType)
        {
            return (flushType == FlushType.EVENTS) ? EventStartIndexName : PeopleStartIndexName;
        }

        private static int NextTrackingId(FlushType flushType)
        {
            return (flushType == FlushType.EVENTS) ? EventAutoIncrementingID() : PeopleAutoIncrementingID();
        }

        private static int CurrentStartIndex(FlushType flushType)
        {
            return (flushType == FlushType.EVENTS) ? EventStartIndex() : PeopleStartIndex();
        }

        private static int AdvanceStartIndex(FlushType flushType, int startIndex, int maxIndex)
        {
            while (startIndex <= maxIndex && !PreferencesSource.HasKey(TrackingKey(flushType, startIndex)))
            {
                startIndex++;
            }

            return startIndex;
        }

        private static int NormalizeStartIndex(FlushType flushType)
        {
            int oldStartIndex = CurrentStartIndex(flushType);
            int newStartIndex = AdvanceStartIndex(flushType, oldStartIndex, NextTrackingId(flushType) - 1);
            if (newStartIndex != oldStartIndex)
            {
                PreferencesSource.SetInt(StartIndexKey(flushType), newStartIndex);
            }

            return newStartIndex;
        }

        internal static void EnqueueTrackingData(Value data, FlushType flushType)
        {
            int trackingDataId = NextTrackingId(flushType);
            String trackingKey = TrackingKey(flushType, trackingDataId);
            data["id"] = trackingKey;
            PreferencesSource.SetString(trackingKey, SerializeStored(data));
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
            int id = NextTrackingId(flushType);
            id += 1;
            PreferencesSource.SetInt(TrackingIdKey(flushType), id);
        }

        internal static StoredBatch DequeueBatchTrackingDataRaw(FlushType flushType, int batchSize)
        {
            List<string> trackingKeys = new List<string>(Math.Max(batchSize, 0));
            StringBuilder payload = new StringBuilder(256);
            payload.Append('[');

            string startIndexKey = StartIndexKey(flushType);
            int oldStartIndex = NormalizeStartIndex(flushType);
            int newStartIndex = oldStartIndex;
            int dataIndex = oldStartIndex;
            int maxIndex = NextTrackingId(flushType) - 1;
            bool wroteItem = false;
            while (trackingKeys.Count < batchSize && dataIndex <= maxIndex) {
                String trackingKey = TrackingKey(flushType, dataIndex);
                if (PreferencesSource.HasKey(trackingKey)) {
                    try {
                        string normalizedPayload = NormalizeStoredPayload(PreferencesSource.GetString(trackingKey));
                        if (wroteItem) payload.Append(", ");
                        payload.Append(normalizedPayload);
                        wroteItem = true;
                        trackingKeys.Add(trackingKey);
                    }
                    catch (Exception e) {
                        Mixpanel.LogError($"There was an error processing stored tracking payload '{trackingKey}': " + e);
                        PreferencesSource.DeleteKey(trackingKey);

                        if (trackingKeys.Count == 0) {
                            // Only update if we didn't find a key prior to deleting this key, since the prior key would be a lower valid index.
                            newStartIndex = Math.Min(dataIndex + 1, maxIndex + 1);
                        }
                    }
                }
                else if (trackingKeys.Count == 0) {
                    // Keep updating the start index as long as we haven't found anything for our batch yet -- we're looking for the minimum index.
                    newStartIndex = Math.Min(dataIndex + 1, maxIndex + 1);
                }
                dataIndex++;
            }

            if (newStartIndex != oldStartIndex) {
                PreferencesSource.SetInt(startIndexKey, newStartIndex);
            }

            payload.Append(']');
            return new StoredBatch(trackingKeys, payload.ToString());
        }

        internal static void DeleteBatchTrackingData(FlushType flushType, int batchSize)
        {
            int deletedCount = 0;
            string startIndexKey = StartIndexKey(flushType);
            int oldStartIndex = CurrentStartIndex(flushType);
            int newStartIndex = oldStartIndex;
            int dataIndex = oldStartIndex;
            int maxIndex = NextTrackingId(flushType) - 1;
            while (deletedCount < batchSize && dataIndex <= maxIndex) {
                String trackingKey = TrackingKey(flushType, dataIndex);
                if (PreferencesSource.HasKey(trackingKey)) {
                    PreferencesSource.DeleteKey(trackingKey);
                    deletedCount++;
                }
                newStartIndex = Math.Min(dataIndex + 1, maxIndex + 1);
                dataIndex++;
            }

            bool deletedAllTrackingData = dataIndex > maxIndex; // if true, we iterated through all events.
            if (deletedAllTrackingData) {
                // For performance reasons, reset the tracking data ID to 0 if all data stored in PlayerPrefs has been deleted.
                // Otherwise, there can be a large number of string concatenation and PreferencesSource.Haskey calls (in extreme cases, 100K+).
                // See https://github.com/mixpanel/mixpanel-unity/pull/152 for context
                string idKey = TrackingIdKey(flushType);
                PreferencesSource.SetInt(idKey, 0);
                PreferencesSource.SetInt(startIndexKey, 0);
            }
            else if (newStartIndex != oldStartIndex) {
                // There are unsent batches, store the index of where to resume searching for next time.
                PreferencesSource.SetInt(startIndexKey, newStartIndex);
            }
        }

        internal static void DeleteBatchTrackingData(FlushType flushType, StoredBatch batch)
        {
            if (batch == null || batch.Count == 0) return;

            foreach (string trackingKey in batch.TrackingKeys) {
                if (PreferencesSource.HasKey(trackingKey)) {
                    PreferencesSource.DeleteKey(trackingKey);
                }
            }

            string startIndexKey = StartIndexKey(flushType);
            int oldStartIndex = CurrentStartIndex(flushType);
            int maxIndex = NextTrackingId(flushType) - 1;
            int newStartIndex = AdvanceStartIndex(flushType, oldStartIndex, maxIndex);

            if (newStartIndex > maxIndex) {
                // Reset the ids when the queue drains so future flushes do not
                // keep scanning sparse, ever-growing PlayerPrefs keys.
                PreferencesSource.SetInt(TrackingIdKey(flushType), 0);
                PreferencesSource.SetInt(startIndexKey, 0);
            }
            else if (newStartIndex != oldStartIndex) {
                PreferencesSource.SetInt(startIndexKey, newStartIndex);
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
                    _onceProperties = DeserializeStored(PreferencesSource.GetString(OncePropertiesName));
                }
                return _onceProperties;
            }
            set
            {
                _onceProperties = value;
                PreferencesSource.SetString(OncePropertiesName, SerializeStored(_onceProperties));
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
                    _superProperties = DeserializeStored(PreferencesSource.GetString(SuperPropertiesName));
                }
                return _superProperties;
            }
            set
            {
                _superProperties = value;
                PreferencesSource.SetString(SuperPropertiesName, SerializeStored(_superProperties));
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
                    _timedEvents = DeserializeStored(PreferencesSource.GetString(TimedEventsName));
                }
                return _timedEvents;
            }
            set
            {
                _timedEvents = value;
                PreferencesSource.SetString(TimedEventsName, SerializeStored(_timedEvents));
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
