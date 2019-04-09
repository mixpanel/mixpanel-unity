using System.Collections.Generic;

namespace mixpanel
{
    public class Value : Dictionary<string, object>
    {   
        public Value() : base() {}
        public Value(int capacity) : base(capacity) {}
    }
}