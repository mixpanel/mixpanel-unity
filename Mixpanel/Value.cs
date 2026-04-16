using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;

namespace mixpanel
{
    [Serializable]
    public class Value : IEnumerable, ISerializationCallbackReceiver
    {


        private enum ValueTypes
        {
            UNDEFINED,
            NULL,
            STRING,
            BOOLEAN,
            NUMBER,
            ARRAY,
            OBJECT
        }

        private enum DataTypes
        {
            UNDEFINED,
            PRIMITIVE,
            CONTAINER,
            URI,
            GUID,
            DATE_TIME,
            DATE_TIME_OFFSET,
            TIME_SPAN,
            COLOR,
            VECTOR,
            QUATERNION,
            BOUNDS,
            RECT
        }

        [SerializeField] private ValueTypes _valueType = ValueTypes.OBJECT;
        [SerializeField] private DataTypes _dataType = DataTypes.UNDEFINED;
        [SerializeField] private string _string;
        [SerializeField] private bool _bool;
        [SerializeField] private double _number;

        // Lazy-initialized backing fields — null until first use.
        // All access goes through the _array/_container properties below.
        [NonSerialized] private List<Value> _arrayBacking;
        [SerializeField] private string[] _arrayData;

        [NonSerialized] private Dictionary<string, Value> _containerBacking;
        [SerializeField] private string[] _containerKeys;
        [SerializeField] private string[] _containerValues;

        private List<Value> _array {
            get => _arrayBacking ?? (_arrayBacking = new List<Value>());
            set => _arrayBacking = value;
        }

        private Dictionary<string, Value> _container {
            get => _containerBacking ?? (_containerBacking = new Dictionary<string, Value>());
            set => _containerBacking = value;
        }

        [ThreadStatic] private static StringBuilder _sharedBuilder;

        public bool IsNull => _valueType == ValueTypes.NULL;
        public bool IsArray => _valueType == ValueTypes.ARRAY;
        public bool IsObject => _valueType == ValueTypes.OBJECT;

        public void OnRecycle()
        {
            _valueType = ValueTypes.OBJECT;
            _dataType = DataTypes.UNDEFINED;
            _string = "";
            _bool = false;
            _number = 0;
            _arrayBacking = null;
            _arrayData = null;
            _containerBacking = null;
            _containerKeys = null;
            _containerValues = null;
        }

        public Value this[int index]
        {
            get => _array[index];
            set
            {
                Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.UNDEFINED,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.ARRAY or ValueTypes.UNDEFINED."
                );
                _valueType = ValueTypes.ARRAY;
                _dataType = DataTypes.CONTAINER;
                _array[index] = value;
            }
        }

        public Value this[string key]
        {
            get
            {
                if (!_container.ContainsKey(key)) _container[key] = new Value();
                return _container[key];
            }
            set
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT || _valueType == ValueTypes.UNDEFINED,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT or ValueTypes.UNDEFINED."
                );
                _valueType = ValueTypes.OBJECT;
                _dataType = DataTypes.CONTAINER;
                _container[key] = value;
            }
        }

        public override string ToString()
        {
            switch (_valueType)
            {
                case ValueTypes.UNDEFINED:
                case ValueTypes.NULL:
                    return "null";
                case ValueTypes.STRING:
                    return _string;
                case ValueTypes.BOOLEAN:
                    return _bool.ToString();
                case ValueTypes.NUMBER:
                    return _number.ToString(CultureInfo.InvariantCulture);
                case ValueTypes.ARRAY:
                case ValueTypes.OBJECT:
                    var sb = _sharedBuilder ?? (_sharedBuilder = new StringBuilder(256));
                    sb.Length = 0;
                    Write(sb);
                    return sb.ToString();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IEnumerator GetEnumerator()
        {
            Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.OBJECT,
                $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.ARRAY or ValueTypes.OBJECT."
            );
            switch (_valueType)
            {
                case ValueTypes.ARRAY:
                    return _array.GetEnumerator();
                case ValueTypes.OBJECT:
                    return _container.GetEnumerator();
            }
            throw new ArgumentOutOfRangeException();
        }

        public int Count
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.OBJECT,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.ARRAY or ValueTypes.OBJECT."
                );
                switch (_valueType)
                {
                    case ValueTypes.ARRAY:
                        return _array.Count;
                    case ValueTypes.OBJECT:
                        return _container.Count;
                }
                throw new ArgumentOutOfRangeException();
            }
        }

        public bool Contains(int index)
        {
            Assert.IsTrue(_valueType == ValueTypes.ARRAY,
                $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.ARRAY."
            );
            return _array.Contains(index);
        }

        public bool ContainsKey(string key)
        {
            Assert.IsTrue(_valueType == ValueTypes.OBJECT,
                $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT."
            );
            return _container.ContainsKey(key);
        }
        
        public void Add(Value value)
        {
            Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.UNDEFINED,
                $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.ARRAY or ValueTypes.UNDEFINED."
            );
            _valueType = ValueTypes.ARRAY;
            _dataType = DataTypes.CONTAINER;
            _array.Add(value);
        }

        public void Add(string key, Value value)
        {
            Assert.IsTrue(_valueType == ValueTypes.OBJECT || _valueType == ValueTypes.UNDEFINED,
                $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT or ValueTypes.UNDEFINED."
            );
            _valueType = ValueTypes.OBJECT;
            _dataType = DataTypes.CONTAINER;
            _container.Add(key, value);
        }

        public void Remove(int index)
        {
            Assert.IsTrue(_valueType == ValueTypes.ARRAY,
                $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.ARRAY."
            );
            _array.Remove(index);
        }

        public void Remove(string key)
        {
            Assert.IsTrue(_valueType == ValueTypes.OBJECT,
                $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT."
            );
            _container.Remove(key);
        }
        
        public IEnumerable<Value> Values
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.OBJECT,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.ARRAY or ValueTypes.OBJECT."
                );
                switch (_valueType)
                {
                    case ValueTypes.ARRAY:
                        return _array;
                    case ValueTypes.OBJECT:
                        return _container.Values;
                }
                throw new ArgumentOutOfRangeException();
            }
        }

        public bool TryGetValue(string key, out Value value)
        {
            Assert.IsTrue(_valueType == ValueTypes.OBJECT,
                $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT."
            );
            return _container.TryGetValue(key, out value);
        }

        public void Merge(Value other)
        {
            Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.OBJECT,
                $"Merge operation failed: _valueType is {_valueType}, but expected ValueTypes.ARRAY or ValueTypes.OBJECT."
            );
            switch (other._valueType)
            {
                case ValueTypes.ARRAY:
                    _array.AddRange(other._array);
                    return;
                case ValueTypes.OBJECT:
                    foreach (string key in other._container.Keys)
                    {
                        _container[key] = other._container[key];
                    }
                    return;
                default:
                    throw new ArgumentException("Unable to merge! Value to merge with is not a 'Array' or 'Object' type.");
            }
        }

        #region Types

        private string String
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.STRING && _dataType == DataTypes.PRIMITIVE,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.STRING or ValueTypes.PRIMITIVE."
                );
                return _string;
            }
            set
            {
                _valueType = ValueTypes.STRING;
                _dataType = DataTypes.PRIMITIVE;
                _string = value;
            }
        }
        
        private bool Bool
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.BOOLEAN && _dataType == DataTypes.PRIMITIVE,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.BOOLEAN or ValueTypes.PRIMITIVE."
                );
                return _bool;
            }
            set
            {
                _valueType = ValueTypes.BOOLEAN;
                _dataType = DataTypes.PRIMITIVE;
                _bool = value;
            }
        }
        
        private double Number
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.NUMBER && _dataType == DataTypes.PRIMITIVE,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.NUMBER or ValueTypes.PRIMITIVE."
                );
                return _number;
            }
            set
            {
                _valueType = ValueTypes.NUMBER;
                _dataType = DataTypes.PRIMITIVE;
                _number = value;
            }
        }
        
        private Uri Uri
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.STRING && _dataType == DataTypes.URI,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.STRING or ValueTypes.URI."
                );
                return new Uri(_string);
            }
            set
            {
                _valueType = ValueTypes.STRING;
                _dataType = DataTypes.URI;
                _string = value.OriginalString;
            }
        }
        
        private Guid Guid
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.STRING && _dataType == DataTypes.GUID,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.STRING or ValueTypes.GUID."
                );
                return Guid.Parse(_string);
            }
            set
            {
                _valueType = ValueTypes.STRING;
                _dataType = DataTypes.GUID;
                _string = value.ToString();
            }
        }
        
        private DateTime DateTime
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.STRING && _dataType == DataTypes.DATE_TIME,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.STRING or ValueTypes.DATE_TIME."
                );
                return DateTime.SpecifyKind(DateTime.Parse(_string), DateTimeKind.Utc);
            }
            set
            {
                _valueType = ValueTypes.STRING;
                _dataType = DataTypes.DATE_TIME;
                _string = DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString(CultureInfo.InvariantCulture);
            }
        }
        
        private DateTimeOffset DateTimeOffset
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.STRING && _dataType == DataTypes.DATE_TIME_OFFSET,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.STRING or ValueTypes.DATE_TIME_OFFSET."
                );
                return DateTimeOffset.Parse(_string);
            }
            set
            {
                _valueType = ValueTypes.STRING;
                _dataType = DataTypes.DATE_TIME_OFFSET;
                _string = value.ToString(CultureInfo.InvariantCulture);
            }
        }
        
        private TimeSpan TimeSpan
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.TIME_SPAN,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT or ValueTypes.TIME_SPAN."
                );
                return new TimeSpan((long)_number);
            }
            set
            {
                _valueType = ValueTypes.OBJECT;
                _dataType = DataTypes.TIME_SPAN;
                _number = value.Ticks;
            }
        }
        
        private Color Color
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.COLOR,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT or ValueTypes.COLOR."
                );
                return new Color(this["r"], this["g"], this["b"], this["a"]);
            }
            set
            {
                _valueType = ValueTypes.OBJECT;
                _dataType = DataTypes.COLOR;
                _container = new Dictionary<string, Value> { {"r", value.r}, {"g", value.g}, {"b", value.b}, {"a", value.a}};
            }
        }
        
        private Color32 Color32
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.COLOR,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT or ValueTypes.COLOR."
                );
                return new Color(this["r"], this["g"], this["b"], this["a"]);
            }
            set
            {
                _valueType = ValueTypes.OBJECT;
                _dataType = DataTypes.COLOR;
                _container = new Dictionary<string, Value> { {"r", value.r}, {"g", value.g}, {"b", value.b}, {"a", value.a}};
            }
        }
        
        private Vector2 Vector2
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.VECTOR,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT or ValueTypes.VECTOR."
                );
                return new Vector2(this["x"], this["y"]);
            }
            set
            {
                _valueType = ValueTypes.OBJECT;
                _dataType = DataTypes.VECTOR;
                _container = new Dictionary<string, Value> { {"x", value.x}, {"y", value.y}};
            }
        }
        
        private Vector3 Vector3
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.VECTOR,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT or ValueTypes.VECTOR."
                );
                return new Vector3(this["x"], this["y"], this["z"]);
            }
            set
            {
                _valueType = ValueTypes.OBJECT;
                _dataType = DataTypes.VECTOR;
                _container = new Dictionary<string, Value> { {"x", value.x}, {"y", value.y}, {"z", value.z}};
            }
        }
        
        private Vector4 Vector4
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && (_dataType == DataTypes.VECTOR || _dataType == DataTypes.QUATERNION),
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT, ValueTypes.VECTOR or ValueTypes.QUATERNION."
                );
                return new Vector4(this["x"], this["y"], this["z"], this["w"]);
            }
            set
            {
                _valueType = ValueTypes.OBJECT;
                _dataType = DataTypes.VECTOR;
                _container = new Dictionary<string, Value> { {"x", value.x}, {"y", value.y}, {"z", value.z}, {"w", value.w}};
            }
        }
        
        private Quaternion Quaternion
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && (_dataType == DataTypes.VECTOR || _dataType == DataTypes.QUATERNION),
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT, ValueTypes.VECTOR or ValueTypes.QUATERNION."
                );
                return new Quaternion(this["x"], this["y"], this["z"], this["w"]);
            }
            set
            {
                _valueType = ValueTypes.OBJECT;
                _dataType = DataTypes.QUATERNION;
                _container = new Dictionary<string, Value> { {"x", value.x}, {"y", value.y}, {"z", value.z}, {"w", value.w}};
            }
        }
        
        private Bounds Bounds
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.BOUNDS,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT or ValueTypes.BOUNDS."
                );
                return new Bounds(this["center"], this["size"]);
            }
            set
            {
                _valueType = ValueTypes.OBJECT;
                _dataType = DataTypes.BOUNDS;
                _container = new Dictionary<string, Value> { {"center", value.center}, {"size", value.size}};
            }
        }
        
        private Rect Rect
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.RECT,
                    $"Assertion failed: _valueType is {_valueType}, but expected ValueTypes.OBJECT or ValueTypes.RECT."
                );
                return new Rect(this["x"], this["y"], this["width"], this["height"]);
            }
            set
            {
                _valueType = ValueTypes.OBJECT;
                _dataType = DataTypes.RECT;
                _container = new Dictionary<string, Value> { {"x", value.x}, {"y", value.y}, {"width", value.width}, {"height", value.height}};
            }
        }

        #endregion
        
        #region Constructors

        public Value() {}

        private Value(ValueTypes valueType, DataTypes dataTypes)
        {
            _valueType = valueType;
            _dataType = dataTypes;
        }

        public Value(string value) { String = value; }
        public Value(bool value) { Bool = value; }
        public Value(double value) { Number = value; }
        public Value(Uri value) { Uri = value; }
        public Value(Guid value) { Guid = value; }
        public Value(DateTime value) { DateTime = value; }
        public Value(DateTimeOffset value) { DateTimeOffset = value; }
        public Value(TimeSpan value) { TimeSpan = value; }
        public Value(Color value) { Color = value; }
        public Value(Color32 value) { Color32 = value; }
        public Value(Vector2 value) { Vector2 = value; }
        public Value(Vector3 value) { Vector3 = value; }
        public Value(Vector4 value) { Vector4 = value; }
        public Value(Quaternion value) { Quaternion = value; }
        public Value(Bounds value) { Bounds = value; }
        public Value(Rect value) { Rect = value; }

        public Value(IEnumerable<Value> data)
        {
            _valueType = ValueTypes.ARRAY;
            _dataType = DataTypes.CONTAINER;
            _array = new List<Value>(data);
        }
        
        public Value(IDictionary<string, Value> data)
        {
            _valueType = ValueTypes.OBJECT;
            _dataType = DataTypes.CONTAINER;
            _container = new Dictionary<string, Value>(data);
        }

        public static Value Null => new Value(ValueTypes.NULL, DataTypes.PRIMITIVE);
        public static Value Array => new Value(ValueTypes.ARRAY, DataTypes.CONTAINER);
        public static Value Object => new Value(ValueTypes.OBJECT, DataTypes.CONTAINER);

        #endregion

        #region ToJsonType
        
        public static implicit operator Value(string value) => new Value(value);
        public static implicit operator Value(string[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<string> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(bool value) => new Value(value);
        public static implicit operator Value(bool[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<bool> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(float value) => new Value((double)(decimal)value);
        public static implicit operator Value(float[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<float> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(double value) => new Value(value);
        public static implicit operator Value(double[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<double> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(decimal value) => new Value((double)value);
        public static implicit operator Value(decimal[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<decimal> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(short value) => new Value(value);
        public static implicit operator Value(short[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<short> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(int value) => new Value(value);
        public static implicit operator Value(int[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<int> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(long value) => new Value(value);
        public static implicit operator Value(long[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<long> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(ushort value) => new Value(value);
        public static implicit operator Value(ushort[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<ushort> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(uint value) => new Value(value);
        public static implicit operator Value(uint[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<uint> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(ulong value) => new Value(value);
        public static implicit operator Value(ulong[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<ulong> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(sbyte value) => new Value(value);
        public static implicit operator Value(sbyte[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<sbyte> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(byte value) => new Value(value);
        public static implicit operator Value(byte[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<byte> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        
        public static implicit operator Value(Uri value) => new Value(value);
        public static implicit operator Value(Uri[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<Uri> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(Guid value) => new Value(value);
        public static implicit operator Value(Guid[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<Guid> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(DateTime value) => new Value(value);
        public static implicit operator Value(DateTime[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<DateTime> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(DateTimeOffset value) => new Value(value);
        public static implicit operator Value(DateTimeOffset[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<DateTimeOffset> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(TimeSpan value) => new Value(value);
        public static implicit operator Value(TimeSpan[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<TimeSpan> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(Color value) => new Value(value);
        public static implicit operator Value(Color[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<Color> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(Color32 value) => new Value(value);
        public static implicit operator Value(Color32[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<Color32> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(Vector2 value) => new Value(value);
        public static implicit operator Value(Vector2[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<Vector2> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(Vector3 value) => new Value(value);
        public static implicit operator Value(Vector3[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<Vector3> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(Vector4 value) => new Value(value);
        public static implicit operator Value(Vector4[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<Vector4> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(Quaternion value) => new Value(value);
        public static implicit operator Value(Quaternion[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<Quaternion> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(Bounds value) => new Value(value);
        public static implicit operator Value(Bounds[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<Bounds> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(Rect value) => new Value(value);
        public static implicit operator Value(Rect[] value) { var v = Value.Array; v._array = new List<Value>(value.Length); for (int i = 0; i < value.Length; i++) v._array.Add(value[i]); return v; }
        public static implicit operator Value(List<Rect> value) { var v = Value.Array; v._array = new List<Value>(value.Count); for (int i = 0; i < value.Count; i++) v._array.Add(value[i]); return v; }
        
        #endregion

        #region ToOtherTypes
        
        public static implicit operator string(Value value) => value.String;
        public static implicit operator string[](Value value) { var r = new string[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (string)value._array[i]; return r; }
        public static implicit operator List<string>(Value value) { var r = new List<string>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((string)value._array[i]); return r; }
        public static implicit operator bool(Value value) => value.Bool;
        public static implicit operator bool[](Value value) { var r = new bool[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (bool)value._array[i]; return r; }
        public static implicit operator List<bool>(Value value) { var r = new List<bool>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((bool)value._array[i]); return r; }
        public static implicit operator float(Value value) => (float)value.Number;
        public static implicit operator float[](Value value) { var r = new float[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (float)value._array[i]; return r; }
        public static implicit operator List<float>(Value value) { var r = new List<float>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((float)value._array[i]); return r; }
        public static implicit operator double(Value value) => value.Number;
        public static implicit operator double[](Value value) { var r = new double[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (double)value._array[i]; return r; }
        public static implicit operator List<double>(Value value) { var r = new List<double>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((double)value._array[i]); return r; }
        public static implicit operator decimal(Value value) => (decimal)value.Number;
        public static implicit operator decimal[](Value value) { var r = new decimal[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (decimal)value._array[i]; return r; }
        public static implicit operator List<decimal>(Value value) { var r = new List<decimal>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((decimal)value._array[i]); return r; }
        public static implicit operator short(Value value) => (short)value.Number;
        public static implicit operator short[](Value value) { var r = new short[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (short)value._array[i]; return r; }
        public static implicit operator List<short>(Value value) { var r = new List<short>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((short)value._array[i]); return r; }
        public static implicit operator int(Value value) => (int)value.Number;
        public static implicit operator int[](Value value) { var r = new int[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (int)value._array[i]; return r; }
        public static implicit operator List<int>(Value value) { var r = new List<int>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((int)value._array[i]); return r; }
        public static implicit operator long(Value value) => (long)value.Number;
        public static implicit operator long[](Value value) { var r = new long[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (long)value._array[i]; return r; }
        public static implicit operator List<long>(Value value) { var r = new List<long>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((long)value._array[i]); return r; }
        public static implicit operator ushort(Value value) => (ushort)value.Number;
        public static implicit operator ushort[](Value value) { var r = new ushort[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (ushort)value._array[i]; return r; }
        public static implicit operator List<ushort>(Value value) { var r = new List<ushort>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((ushort)value._array[i]); return r; }
        public static implicit operator uint(Value value) => (uint)value.Number;
        public static implicit operator uint[](Value value) { var r = new uint[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (uint)value._array[i]; return r; }
        public static implicit operator List<uint>(Value value) { var r = new List<uint>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((uint)value._array[i]); return r; }
        public static implicit operator ulong(Value value) => (ulong)value.Number;
        public static implicit operator ulong[](Value value) { var r = new ulong[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (ulong)value._array[i]; return r; }
        public static implicit operator List<ulong>(Value value) { var r = new List<ulong>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((ulong)value._array[i]); return r; }
        public static implicit operator sbyte(Value value) => (sbyte)value.Number;
        public static implicit operator sbyte[](Value value) { var r = new sbyte[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (sbyte)value._array[i]; return r; }
        public static implicit operator List<sbyte>(Value value) { var r = new List<sbyte>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((sbyte)value._array[i]); return r; }
        public static implicit operator byte(Value value) => (byte)value.Number;
        public static implicit operator byte[](Value value) { var r = new byte[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (byte)value._array[i]; return r; }
        public static implicit operator List<byte>(Value value) { var r = new List<byte>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((byte)value._array[i]); return r; }
        
        public static implicit operator Uri(Value value) => value.Uri;
        public static implicit operator Uri[](Value value) { var r = new Uri[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (Uri)value._array[i]; return r; }
        public static implicit operator List<Uri>(Value value) { var r = new List<Uri>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((Uri)value._array[i]); return r; }
        public static implicit operator Guid(Value value) => value.Guid;
        public static implicit operator Guid[](Value value) { var r = new Guid[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (Guid)value._array[i]; return r; }
        public static implicit operator List<Guid>(Value value) { var r = new List<Guid>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((Guid)value._array[i]); return r; }
        public static implicit operator DateTime(Value value) => value.DateTime;
        public static implicit operator DateTime[](Value value) { var r = new DateTime[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (DateTime)value._array[i]; return r; }
        public static implicit operator List<DateTime>(Value value) { var r = new List<DateTime>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((DateTime)value._array[i]); return r; }
        public static implicit operator DateTimeOffset(Value value) => value.DateTimeOffset;
        public static implicit operator DateTimeOffset[](Value value) { var r = new DateTimeOffset[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (DateTimeOffset)value._array[i]; return r; }
        public static implicit operator List<DateTimeOffset>(Value value) { var r = new List<DateTimeOffset>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((DateTimeOffset)value._array[i]); return r; }
        public static implicit operator TimeSpan(Value value) => value.TimeSpan;
        public static implicit operator TimeSpan[](Value value) { var r = new TimeSpan[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (TimeSpan)value._array[i]; return r; }
        public static implicit operator List<TimeSpan>(Value value) { var r = new List<TimeSpan>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((TimeSpan)value._array[i]); return r; }
        public static implicit operator Color(Value value) => value.Color;
        public static implicit operator Color[](Value value) { var r = new Color[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (Color)value._array[i]; return r; }
        public static implicit operator List<Color>(Value value) { var r = new List<Color>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((Color)value._array[i]); return r; }
        public static implicit operator Color32(Value value) => value.Color32;
        public static implicit operator Color32[](Value value) { var r = new Color32[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (Color32)value._array[i]; return r; }
        public static implicit operator List<Color32>(Value value) { var r = new List<Color32>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((Color32)value._array[i]); return r; }
        public static implicit operator Vector2(Value value) => value.Vector2;
        public static implicit operator Vector2[](Value value) { var r = new Vector2[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (Vector2)value._array[i]; return r; }
        public static implicit operator List<Vector2>(Value value) { var r = new List<Vector2>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((Vector2)value._array[i]); return r; }
        public static implicit operator Vector3(Value value) => value.Vector3;
        public static implicit operator Vector3[](Value value) { var r = new Vector3[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (Vector3)value._array[i]; return r; }
        public static implicit operator List<Vector3>(Value value) { var r = new List<Vector3>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((Vector3)value._array[i]); return r; }
        public static implicit operator Vector4(Value value) => value.Vector4;
        public static implicit operator Vector4[](Value value) { var r = new Vector4[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (Vector4)value._array[i]; return r; }
        public static implicit operator List<Vector4>(Value value) { var r = new List<Vector4>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((Vector4)value._array[i]); return r; }
        public static implicit operator Quaternion(Value value) => value.Quaternion;
        public static implicit operator Quaternion[](Value value) { var r = new Quaternion[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (Quaternion)value._array[i]; return r; }
        public static implicit operator List<Quaternion>(Value value) { var r = new List<Quaternion>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((Quaternion)value._array[i]); return r; }
        public static implicit operator Bounds(Value value) => value.Bounds;
        public static implicit operator Bounds[](Value value) { var r = new Bounds[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (Bounds)value._array[i]; return r; }
        public static implicit operator List<Bounds>(Value value) { var r = new List<Bounds>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((Bounds)value._array[i]); return r; }
        public static implicit operator Rect(Value value) => value.Rect;
        public static implicit operator Rect[](Value value) { var r = new Rect[value._array.Count]; for (int i = 0; i < r.Length; i++) r[i] = (Rect)value._array[i]; return r; }
        public static implicit operator List<Rect>(Value value) { var r = new List<Rect>(value._array.Count); for (int i = 0; i < value._array.Count; i++) r.Add((Rect)value._array[i]); return r; }
        
        #endregion

        #region Writer

        private void Write(StringBuilder sb, bool includeTypeInfo = false)
        {
            if (includeTypeInfo)
            {
                sb.Append("{\"JsonType\": \"");
                sb.Append(_valueType);
                sb.Append("\", \"DataType\": \"");
                sb.Append(_dataType);
                sb.Append("\", \"Value\": ");
            }
            switch (_valueType)
            {
                case ValueTypes.UNDEFINED:
                case ValueTypes.NULL:
                    sb.Append("null");
                    break;
                case ValueTypes.STRING:
                    sb.Append('"');
                    AppendSanitizedString(sb, _string);
                    sb.Append('"');
                    break;
                case ValueTypes.BOOLEAN:
                    sb.Append(_bool ? "true" : "false");
                    break;
                case ValueTypes.NUMBER:
                    sb.Append(_number.ToString(CultureInfo.InvariantCulture));
                    break;
                case ValueTypes.ARRAY:
                    sb.Append('[');
                    for (int i = 0; i < _array.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        _array[i].Write(sb, includeTypeInfo);
                    }
                    sb.Append(']');
                    break;
                case ValueTypes.OBJECT:
                    sb.Append('{');
                    int idx = 0;
                    foreach (KeyValuePair<string, Value> kvp in _container)
                    {
                        if (idx > 0) sb.Append(", ");
                        sb.Append('"');
                        sb.Append(kvp.Key);
                        sb.Append("\": ");
                        kvp.Value.Write(sb, includeTypeInfo);
                        idx++;
                    }
                    sb.Append('}');
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            if (includeTypeInfo) sb.Append('}');
        }
        
        #endregion
        
        #region Reader

        private static void AppendSanitizedString(StringBuilder sb, string s)
        {
            if (s == null || s.Length == 0) return;
            for (int i = 0; i < s.Length; i += 1) {
                char c = s[i];
                if (c >= 0 && c <= 7 || c == 11 || c >= 14 && c <= 31 || c == 39 || c == 60 || c == 62)
                    sb.AppendFormat("\\u{0:x4}", (int)c);
                else switch (c) {
                    case '\\':
                    case '"':
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
        }

        public static string SanitizeStringForJson(string s)
        {
            if (s == null || s.Length == 0) {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            AppendSanitizedString(sb, s);
            return sb.ToString();
        }
        
        #endregion

        #region UnitySerialization
        public void OnBeforeSerialize()
        {
            if (IsArray) SerializeList();
            if (IsObject) SerializeDictionary();
        }

        private void SerializeList()
        {
            int count = _array.Count;
            _arrayData = new string[count];
            if (count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                _arrayData[i] = JsonUtility.ToJson(_array[i]); 
            }
        }

        private void SerializeDictionary()
        {
            int count = _container.Count;
            _containerKeys = new string[count];
            _containerValues = new string[count];
            if (count <= 0) return;
            using (Dictionary<string, Value>.Enumerator e = _container.GetEnumerator())
            {
                for (int i = 0; i < count; i++)
                {
                    e.MoveNext();
                    _containerKeys[i] = e.Current.Key;
                    _containerValues[i] = JsonUtility.ToJson(e.Current.Value); 
                }
            }
        }

        public void OnAfterDeserialize()
        {
            if (IsArray) DeserializeList();
            if (IsObject) DeserializeDictionary();
        }

        private void DeserializeList()
        {
            if (_arrayData == null) return;
            int count = _arrayData.Length;
            _array = new List<Value>(count);
            if (count == 0) return;
            foreach (string data in _arrayData)
            {
                Value item = new Value();
                JsonUtility.FromJsonOverwrite(data, item);
                _array.Add(item);
            }
            _arrayData = null;
        }

        private void DeserializeDictionary()
        {
            if (_containerKeys == null) return;
            int count = _containerKeys.Length;
            _container = new Dictionary<string, Value>(count);
            if (count == 0) return;
            for (int i = 0; i < count; i++)
            {
                Value item = new Value();
                JsonUtility.FromJsonOverwrite(_containerValues[i], item);
                _container[_containerKeys[i]] = item;
            }
            _containerKeys = null;
            _containerValues = null;
        }
        #endregion

        // Only used to migrate from 1.X to 2.X - Will be removed soon
        #region Deserialize

        public static Value Deserialize(string json)
        {
            return ParseValue(new StringReader(json));
        }

        private static Value ParseValue(StringReader reader)
        {
            return ParseByToken(reader, NextToken(reader));
        }

        private static Value ParseByToken(StringReader reader, Token token)
        {
            switch (token) {
                case Token.STRING:
                    return ParseString(reader);
                case Token.NUMBER:
                    return ParseNumber(reader);
                case Token.CURLY_OPEN:
                    // ditch opening brace
                    reader.Read();
                    return ParseObject(reader);
                case Token.SQUARED_OPEN:
                    // ditch opening bracket
                    reader.Read();
                    return ParseArray(reader);
                case Token.TRUE:
                    return true;
                case Token.FALSE:
                    return false;
                case Token.NULL:
                    return Value.Null;
                default:
                    return Value.Null;
            }
        }

        private static string ParseString(StringReader reader)
        {
            StringBuilder s = new StringBuilder();
            
            // ditch opening quote
            reader.Read();
            
            bool parsing = true;
            while (parsing) {

                if (reader.Peek() == -1) {
                    parsing = false;
                    break;
                }

                char c = NextChar(reader);
                switch (c) {
                case '"':
                    parsing = false;
                    break;
                case '\\':
                    if (reader.Peek() == -1) {
                        parsing = false;
                        break;
                    }

                    c = NextChar(reader);
                    switch (c) {
                        case '"':
                        case '\\':
                        case '/':
                            s.Append(c);
                            break;
                        case 'b':
                            s.Append('\b');
                            break;
                        case 'f':
                            s.Append('\f');
                            break;
                        case 'n':
                            s.Append('\n');
                            break;
                        case 'r':
                            s.Append('\r');
                            break;
                        case 't':
                            s.Append('\t');
                            break;
                        case 'u':
                            StringBuilder hex = new StringBuilder();

                            for (int i=0; i< 4; i++) {
                                hex.Append(NextChar(reader));
                            }

                            s.Append((char) Convert.ToInt32(hex.ToString(), 16));
                            break;
                    }
                    break;
                default:
                    s.Append(c);
                    break;
                }
            }

            return s.ToString();
        }
        
        private static double ParseNumber(StringReader reader)
        {
            string number = NextWord(reader);
            double parsedDouble;
            double.TryParse(number, out parsedDouble);
            return parsedDouble;
        }
        
        private static Value ParseArray(StringReader reader)
        {
            List<Value> array = new List<Value>();

            bool parsing = true;
            while (parsing) {
                Token nextToken = NextToken(reader);

                switch (nextToken) {
                    case Token.NONE:
                        return null;
                    case Token.COMMA:
                        continue;
                    case Token.SQUARED_CLOSE:
                        parsing = false;
                        break;
                    default:
                        array.Add(ParseByToken(reader, nextToken));
                        break;
                }
            }

            return new Value(array);
        }
        
        private static Value ParseObject(StringReader reader)
        {
            Dictionary<string, Value> data = new Dictionary<string, Value>();

            while (true) {
                switch (NextToken(reader)) {
                    case Token.NONE:
                        return null;
                    case Token.COMMA:
                        continue;
                    case Token.CURLY_CLOSE:
                        if (data.ContainsKey("JsonType") && data.ContainsKey("DataType"))
                        {
                            return FromSerialization(data["JsonType"], data["DataType"], data["Value"]);
                        }
                        return new Value(data);
                    default:
                        // key
                        string key = ParseString(reader);
                        if (key == null) {
                            return null;
                        }
                        // :
                        if (NextToken(reader) != Token.COLON) {
                            return null;
                        }
                        // ditch the colon
                        reader.Read();

                        // value
                        data[key] = ParseValue(reader);
                        break;
                }
            }
        }

        private const string WhiteSpace = " \t\n\r";
        private const string WordBreak = " \t\n\r{}[],:\"";

        private enum Token
        {
            NONE,
            CURLY_OPEN,
            CURLY_CLOSE,
            SQUARED_OPEN,
            SQUARED_CLOSE,
            COLON,
            COMMA,
            STRING,
            NUMBER,
            TRUE,
            FALSE,
            NULL
        };
        
        private static char PeekChar(StringReader reader) => Convert.ToChar(reader.Peek());

        private static char NextChar(StringReader reader) => Convert.ToChar(reader.Read());

        private static string NextWord(StringReader reader)
        {
            StringBuilder word = new StringBuilder();
            while (WordBreak.IndexOf(PeekChar(reader)) == -1) {
                word.Append(NextChar(reader));

                if (reader.Peek() == -1) {
                    break;
                }
            }
            return word.ToString();
        }

        private static void EatWhitespace(StringReader reader)
        {
            while (WhiteSpace.IndexOf(PeekChar(reader)) != -1) {
                reader.Read();

                if (reader.Peek() == -1) {
                    break;
                }
            }
        }

        private static Token NextToken(StringReader reader)
        {
            EatWhitespace(reader);

            if (reader.Peek() == -1) {
                return Token.NONE;
            }

            char c = PeekChar(reader);
            switch (c) {
                case '{':
                    return Token.CURLY_OPEN;
                case '}':
                    reader.Read();
                    return Token.CURLY_CLOSE;
                case '[':
                    return Token.SQUARED_OPEN;
                case ']':
                    reader.Read();
                    return Token.SQUARED_CLOSE;
                case ',':
                    reader.Read();
                    return Token.COMMA;
                case '"':
                    return Token.STRING;
                case ':':
                    return Token.COLON;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '-':
                    return Token.NUMBER;
            }

            string word = NextWord(reader);

            switch (word) {
                case "false":
                    return Token.FALSE;
                case "true":
                    return Token.TRUE;
                case "null":
                    return Token.NULL;
            }

            return Token.NONE;
        }

        private static Value FromSerialization(Value valueType, Value dataType, Value value)
        {
            value._valueType = (ValueTypes) Enum.Parse(typeof(ValueTypes), valueType);
            value._dataType = (DataTypes) Enum.Parse(typeof(DataTypes), dataType);
            return value;
        }
        #endregion

    }
}
