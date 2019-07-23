using System;
using System.Collections.Generic;
using System.IO;
using mixpanel.serialization;
using UnityEngine;

namespace mixpanel
{
    public static partial class Mixpanel
    {
        [Serializable]
        internal class MixpanelData
        {
            public string distinctId = Guid.NewGuid().ToString();
            public bool isTracking = true;
            public Value superProperties = Value.Object;
            public Value timedEvents = Value.Object;
            public Dictionary<string, Dictionary<string, MixpanelRequest>> buffer = new Dictionary<string, Dictionary<string, MixpanelRequest>>();
        }
        
        private static string DataPersistencePath => Path.Combine(Application.persistentDataPath, "mixpanel.dat");


        private static MixpanelData _data;

        internal static MixpanelData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static string DistinctId
        {
            get => Data.distinctId;
            set
            {
                Data.distinctId = value;
                Save();
            }
        }
        [Obsolete("Please use 'DistinctId' instead!")]
        public static string DistinctID
        {
            get => DistinctId;
            set => DistinctId = value;
        }

        public static bool IsTracking
        {
            get => Data.isTracking;
            set
            {
                Data.isTracking = value;
                Save();
            }
        }

        internal static Value SuperProperties
        {
            get => Data.superProperties;
            set
            {
                Data.superProperties = value;
                Save();
            }
        }

        internal static Value TimedEvents
        {
            get => Data.timedEvents;
            set
            {
                Data.timedEvents = value;
                Save();
            }
        }
    }
}
