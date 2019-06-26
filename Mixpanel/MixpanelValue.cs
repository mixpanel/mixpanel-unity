using System;
using System.Collections.Generic;

namespace mixpanel
{
    [Serializable]
    public class Value : Dictionary<string, object>
    {
        public Value() {}
        
        public Value(int capacity) : base(capacity) {}
        
        public Value(Dictionary<string, object> data)
        {
            foreach (KeyValuePair<string, object> kvp in data)
            {
                this[kvp.Key] = kvp.Value;
            }
        }
    }
}