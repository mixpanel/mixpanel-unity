using System;
using System.Collections.Generic;
using UnityEngine;

namespace mixpanel
{
    [Serializable]
    public class Value : Dictionary<string, object>
    {
        public Value() : base() {}
        public Value(int capacity) : base(capacity) {}
        public Value(Dictionary<string, object> data)
        {
            foreach (var kvp in data)
            {
                this[kvp.Key] = kvp.Value;
            }
        }
    }
}