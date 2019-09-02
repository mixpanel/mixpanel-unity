using System;
using System.Collections.Concurrent;

namespace mixpanel
{
    internal interface IMixpanelPoolable
    {
        void OnRecycle();
    }
    
    internal class MixpanelPool<T> where T : IMixpanelPoolable
    {
        private readonly Func<T> _objectGenerator;
        private readonly ConcurrentBag<T> _objects;

        public MixpanelPool(Func<T> objectGenerator)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _objects = new ConcurrentBag<T>();
        }

        public T Get()
        {
            return !_objects.TryTake(out T item) ? _objectGenerator() : item;
        }

        public void Put(T item)
        {
            item.OnRecycle();
            _objects.Add(item);
        }

        public int Count => _objects.Count;
    }
    
    public static partial class Mixpanel
    {
        internal static readonly MixpanelPool<Value> NullPool = new MixpanelPool<Value> (() => Value.Null);
        internal static readonly MixpanelPool<Value> ArrayPool = new MixpanelPool<Value> (() => Value.Array);
        internal static readonly MixpanelPool<Value> ObjectPool = new MixpanelPool<Value> (() => Value.Object);

        internal static void Put(Value value)
        {
            if (value.IsObject)
            {
                if (ObjectPool.Count < 5000) ObjectPool.Put(value);
                return;
            }
            if (value.IsArray)
            {
                if (ArrayPool.Count < 5000) ArrayPool.Put(value);
                return;
            }
            if (value.IsNull)
            {
                if (NullPool.Count < 100) NullPool.Put(value);
                return;
            }
        }
    }
}
