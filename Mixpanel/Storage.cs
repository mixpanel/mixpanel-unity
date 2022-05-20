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

        #region HasIntegratedLibrary

        private const string HasIntegratedLibraryName = "Mixpanel.HasIntegratedLibrary";

        internal static bool HasIntegratedLibrary
        {
            get => Convert.ToBoolean(PreferencesSource.GetInt(HasIntegratedLibraryName, 0));
            set => PreferencesSource.SetInt(HasIntegratedLibraryName, Convert.ToInt32(value));
        }

        #endregion

        #region MPDebugInitCount

        private const string MPDebugInitCountName = "Mixpanel.MPDebugInitCount";

        internal static int MPDebugInitCount
        {
            get => PreferencesSource.GetInt(MPDebugInitCountName, 0);
            set => PreferencesSource.SetInt(MPDebugInitCountName, value);
        }

        #endregion

        #region HasImplemented

        private const string HasImplementedName = "Mixpanel.HasImplemented";

        internal static bool HasImplemented
        {
            get => Convert.ToBoolean(PreferencesSource.GetInt(HasImplementedName, 0));
            set => PreferencesSource.SetInt(HasImplementedName, Convert.ToInt32(value));
        }
        
        #endregion

        #region HasTracked

        private const string HasTrackedName = "Mixpanel.HasTracked";

        internal static bool HasTracked
        {
            get => Convert.ToBoolean(PreferencesSource.GetInt(HasTrackedName, 0));
            set => PreferencesSource.SetInt(HasTrackedName, Convert.ToInt32(value));
        }

        #endregion

        #region HasIdentified

        private const string HasIdentifiedName = "Mixpanel.HasIdentified";

        internal static bool HasIdendified
        {
            get => Convert.ToBoolean(PreferencesSource.GetInt(HasIdentifiedName, 0));
            set => PreferencesSource.SetInt(HasIdentifiedName, Convert.ToInt32(value));
        }

        #endregion

        #region HasAliased

        private const string HasAliasedName = "Mixpanel.HasAliased";

        internal static bool HasAliased
        {
            get => Convert.ToBoolean(PreferencesSource.GetInt(HasAliasedName, 0));
            set => PreferencesSource.SetInt(HasAliasedName, Convert.ToInt32(value));
        }

        #endregion

        #region HasUsedPeople

        private const string HasUsedPeopleName = "Mixpanel.HasUsedPeople";

        internal static bool HasUsedPeople
        {
            get => Convert.ToBoolean(PreferencesSource.GetInt(HasUsedPeopleName, 0));
            set => PreferencesSource.SetInt(HasUsedPeopleName, Convert.ToInt32(value));
        }

        #endregion

        #region HasTrackedFirstSDKDebugLaunch

        private const string HasTrackedFirstSDKDebugLaunchName = "Mixpanel.HasTrackedFirstSDKDebugLaunch";

        internal static bool HasTrackedFirstSDKDebugLaunch
        {
            get => Convert.ToBoolean(PreferencesSource.GetInt(HasTrackedFirstSDKDebugLaunchName, 0));
            set => PreferencesSource.SetInt(HasTrackedFirstSDKDebugLaunchName, Convert.ToInt32(value));
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
            return PreferencesSource.HasKey("EventAutoIncrementingID") ? PreferencesSource.GetInt("EventAutoIncrementingID") : 0;
        }

        internal static int PeopleAutoIncrementingID()
        {
            return PreferencesSource.HasKey("PeopleAutoIncrementingID") ? PreferencesSource.GetInt("PeopleAutoIncrementingID") : 0;
        }

        private static void IncreaseTrackingDataID(FlushType flushType)
        {
            int id = (flushType == FlushType.EVENTS)? EventAutoIncrementingID() : PeopleAutoIncrementingID();
            id += 1;
            String trackingIdKey = (flushType == FlushType.EVENTS)? "EventAutoIncrementingID" : "PeopleAutoIncrementingID";
            PreferencesSource.SetInt(trackingIdKey, id);
        }

        internal static Value DequeueBatchTrackingData(FlushType flushType, int batchSize)
        {
            Value batch = Value.Array;
            int dataIndex = 0;
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
                    }
                }
                dataIndex++;
            }
            
            return batch;
        }

        internal static void DeleteBatchTrackingData(FlushType flushType, int batchSize)
        {
            int deletedCount = 0;
            int dataIndex = 0;
            int maxIndex = (flushType == FlushType.EVENTS) ? EventAutoIncrementingID() - 1 : PeopleAutoIncrementingID() - 1;
            while (deletedCount < batchSize && dataIndex <= maxIndex) {
                String trackingKey = (flushType == FlushType.EVENTS) ? "Event" + dataIndex.ToString() : "People" + dataIndex.ToString();    
                if (PreferencesSource.HasKey(trackingKey)) {
                    PreferencesSource.DeleteKey(trackingKey);
                    deletedCount++;
                }
                dataIndex++;
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
