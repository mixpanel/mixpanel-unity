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
    public class Value : IEnumerable
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

        private ValueTypes _valueType = ValueTypes.OBJECT;
        private DataTypes _dataType = DataTypes.UNDEFINED;
        
        private string _string;
        private bool _bool;
        private double _number;
        
        private List<Value> _array = new List<Value>();
        private Dictionary<string, Value> _container = new Dictionary<string, Value>();

        public bool IsArray => _valueType == ValueTypes.ARRAY;
        public bool IsObject => _valueType == ValueTypes.OBJECT;

        public Value this[int index]
        {
            get => _array[index];
            set
            {
                Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.UNDEFINED);
                _valueType = ValueTypes.ARRAY;
                _dataType = DataTypes.CONTAINER;
                _array[index] = value;
            }
        }

        public Value this[string key]
        {
            get
            {
                if (!_container.ContainsKey(key)) _container[key] = Value.Object;
                return _container[key];
            }
            set
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT || _valueType == ValueTypes.UNDEFINED);
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
                    return _number.ToString(CultureInfo.CurrentCulture);
                case ValueTypes.ARRAY:
                    StringWriter arrayWriter = new StringWriter();
                    Write(arrayWriter);
                    return arrayWriter.ToString();
                case ValueTypes.OBJECT:
                    StringWriter containerWriter = new StringWriter();
                    Write(containerWriter);
                    return containerWriter.ToString();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IEnumerator GetEnumerator()
        {
            Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.OBJECT);
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
                Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.OBJECT);
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
            Assert.IsTrue(_valueType == ValueTypes.ARRAY);
            return _array.Contains(index);
        }

        public bool ContainsKey(string key)
        {
            Assert.IsTrue(_valueType == ValueTypes.OBJECT);
            return _container.ContainsKey(key);
        }
        
        public void Add(Value value)
        {
            Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.UNDEFINED);
            _valueType = ValueTypes.ARRAY;
            _dataType = DataTypes.CONTAINER;
            _array.Add(value);
        }

        public void Add(string key, Value value)
        {
            Assert.IsTrue(_valueType == ValueTypes.OBJECT || _valueType == ValueTypes.UNDEFINED);
            _valueType = ValueTypes.OBJECT;
            _dataType = DataTypes.CONTAINER;
            _container.Add(key, value);
        }

        public void Remove(int index)
        {
            Assert.IsTrue(_valueType == ValueTypes.ARRAY);
            _array.Remove(index);
        }

        public void Remove(string key)
        {
            Assert.IsTrue(_valueType == ValueTypes.OBJECT);
            _container.Remove(key);
        }
        
        public IEnumerable<Value> Values
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.OBJECT);
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
            Assert.IsTrue(_valueType == ValueTypes.OBJECT);
            return _container.TryGetValue(key, out value);
        }

        public void Merge(Value other)
        {
            Assert.IsTrue(_valueType == ValueTypes.ARRAY || _valueType == ValueTypes.OBJECT);
            switch (other._valueType)
            {
                case ValueTypes.ARRAY:
                    foreach (Value item in other._array)
                    {
                        _array.Add(item);
                    }
                    return;
                case ValueTypes.OBJECT:
                    foreach (KeyValuePair<string, Value> item in other._container)
                    {
                        _container.Add(item.Key, item.Value);
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
                Assert.IsTrue(_valueType == ValueTypes.STRING && _dataType == DataTypes.PRIMITIVE);
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
                Assert.IsTrue(_valueType == ValueTypes.BOOLEAN && _dataType == DataTypes.PRIMITIVE);
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
                Assert.IsTrue(_valueType == ValueTypes.NUMBER && _dataType == DataTypes.PRIMITIVE);
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
                Assert.IsTrue(_valueType == ValueTypes.STRING && _dataType == DataTypes.URI);
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
                Assert.IsTrue(_valueType == ValueTypes.STRING && _dataType == DataTypes.GUID);
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
                Assert.IsTrue(_valueType == ValueTypes.STRING && _dataType == DataTypes.DATE_TIME);
                return DateTime.SpecifyKind(DateTime.Parse(_string), DateTimeKind.Utc);
            }
            set
            {
                _valueType = ValueTypes.STRING;
                _dataType = DataTypes.DATE_TIME;
                _string = DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString(CultureInfo.CurrentCulture);
            }
        }
        
        private DateTimeOffset DateTimeOffset
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.STRING && _dataType == DataTypes.DATE_TIME_OFFSET);
                return DateTimeOffset.Parse(_string);
            }
            set
            {
                _valueType = ValueTypes.STRING;
                _dataType = DataTypes.DATE_TIME_OFFSET;
                _string = value.ToString(CultureInfo.CurrentCulture);
            }
        }
        
        private TimeSpan TimeSpan
        {
            get
            {
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.TIME_SPAN);
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
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.COLOR);
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
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.COLOR);
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
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.VECTOR);
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
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.VECTOR);
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
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && (_dataType == DataTypes.VECTOR || _dataType == DataTypes.QUATERNION));
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
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && (_dataType == DataTypes.VECTOR || _dataType == DataTypes.QUATERNION));
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
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.BOUNDS);
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
                Assert.IsTrue(_valueType == ValueTypes.OBJECT && _dataType == DataTypes.RECT);
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

        public static Value Null => new Value { _valueType = ValueTypes.NULL, _dataType = DataTypes.PRIMITIVE };
        public static Value Array => new Value { _valueType = ValueTypes.ARRAY, _dataType = DataTypes.CONTAINER };
        public static Value Object => new Value { _valueType = ValueTypes.OBJECT, _dataType = DataTypes.CONTAINER };

        #endregion

        #region ToJsonType
        
        public static implicit operator Value(string value) => new Value(value);
        public static implicit operator Value(string[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<string> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(bool value) => new Value(value);
        public static implicit operator Value(bool[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<bool> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(float value) => new Value((double)(decimal)value);
        public static implicit operator Value(float[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<float> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(double value) => new Value(value);
        public static implicit operator Value(double[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<double> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(decimal value) => new Value((double)value);
        public static implicit operator Value(decimal[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<decimal> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(short value) => new Value(value);
        public static implicit operator Value(short[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<short> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(int value) => new Value(value);
        public static implicit operator Value(int[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<int> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(long value) => new Value(value);
        public static implicit operator Value(long[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<long> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(ushort value) => new Value(value);
        public static implicit operator Value(ushort[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<ushort> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(uint value) => new Value(value);
        public static implicit operator Value(uint[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<uint> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(ulong value) => new Value(value);
        public static implicit operator Value(ulong[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<ulong> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(sbyte value) => new Value(value);
        public static implicit operator Value(sbyte[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<sbyte> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(byte value) => new Value(value);
        public static implicit operator Value(byte[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<byte> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        
        public static implicit operator Value(Uri value) => new Value(value);
        public static implicit operator Value(Uri[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<Uri> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(Guid value) => new Value(value);
        public static implicit operator Value(Guid[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<Guid> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(DateTime value) => new Value(value);
        public static implicit operator Value(DateTime[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<DateTime> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(DateTimeOffset value) => new Value(value);
        public static implicit operator Value(DateTimeOffset[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<DateTimeOffset> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(TimeSpan value) => new Value(value);
        public static implicit operator Value(TimeSpan[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<TimeSpan> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(Color value) => new Value(value);
        public static implicit operator Value(Color[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<Color> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(Color32 value) => new Value(value);
        public static implicit operator Value(Color32[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<Color32> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(Vector2 value) => new Value(value);
        public static implicit operator Value(Vector2[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<Vector2> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(Vector3 value) => new Value(value);
        public static implicit operator Value(Vector3[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<Vector3> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(Vector4 value) => new Value(value);
        public static implicit operator Value(Vector4[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<Vector4> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(Quaternion value) => new Value(value);
        public static implicit operator Value(Quaternion[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<Quaternion> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(Bounds value) => new Value(value);
        public static implicit operator Value(Bounds[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<Bounds> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        public static implicit operator Value(Rect value) => new Value(value);
        public static implicit operator Value(Rect[] value) => new Value(System.Array.ConvertAll(value, x => (Value)x));
        public static implicit operator Value(List<Rect> value) => new Value(System.Array.ConvertAll(value.ToArray(), x => (Value)x));
        
        #endregion

        #region ToOtherTypes
        
        public static implicit operator string(Value value) => value.String;
        public static implicit operator string[](Value value) => value._array.ConvertAll(x => (string)x).ToArray();
        public static implicit operator List<string>(Value value) => value._array.ConvertAll(x => (string)x);
        public static implicit operator bool(Value value) => value.Bool;
        public static implicit operator bool[](Value value) => value._array.ConvertAll(x => (bool)x).ToArray();
        public static implicit operator List<bool>(Value value) => value._array.ConvertAll(x => (bool)x);
        public static implicit operator float(Value value) => (float)value.Number;
        public static implicit operator float[](Value value) => value._array.ConvertAll(x => (float)x).ToArray();
        public static implicit operator List<float>(Value value) => value._array.ConvertAll(x => (float)x);
        public static implicit operator double(Value value) => value.Number;
        public static implicit operator double[](Value value) => value._array.ConvertAll(x => (double)x).ToArray();
        public static implicit operator List<double>(Value value) => value._array.ConvertAll(x => (double)x);
        public static implicit operator decimal(Value value) => (decimal)value.Number;
        public static implicit operator decimal[](Value value) => value._array.ConvertAll(x => (decimal)x).ToArray();
        public static implicit operator List<decimal>(Value value) => value._array.ConvertAll(x => (decimal)x);
        public static implicit operator short(Value value) => (short)value.Number;
        public static implicit operator short[](Value value) => value._array.ConvertAll(x => (short)x).ToArray();
        public static implicit operator List<short>(Value value) => value._array.ConvertAll(x => (short)x);
        public static implicit operator int(Value value) => (int)value.Number;
        public static implicit operator int[](Value value) => value._array.ConvertAll(x => (int)x).ToArray();
        public static implicit operator List<int>(Value value) => value._array.ConvertAll(x => (int)x);
        public static implicit operator long(Value value) => (long)value.Number;
        public static implicit operator long[](Value value) => value._array.ConvertAll(x => (long)x).ToArray();
        public static implicit operator List<long>(Value value) => value._array.ConvertAll(x => (long)x);
        public static implicit operator ushort(Value value) => (ushort)value.Number;
        public static implicit operator ushort[](Value value) => value._array.ConvertAll(x => (ushort)x).ToArray();
        public static implicit operator List<ushort>(Value value) => value._array.ConvertAll(x => (ushort)x);
        public static implicit operator uint(Value value) => (uint)value.Number;
        public static implicit operator uint[](Value value) => value._array.ConvertAll(x => (uint)x).ToArray();
        public static implicit operator List<uint>(Value value) => value._array.ConvertAll(x => (uint)x);
        public static implicit operator ulong(Value value) => (ulong)value.Number;
        public static implicit operator ulong[](Value value) => value._array.ConvertAll(x => (ulong)x).ToArray();
        public static implicit operator List<ulong>(Value value) => value._array.ConvertAll(x => (ulong)x);
        public static implicit operator sbyte(Value value) => (sbyte)value.Number;
        public static implicit operator sbyte[](Value value) => value._array.ConvertAll(x => (sbyte)x).ToArray();
        public static implicit operator List<sbyte>(Value value) => value._array.ConvertAll(x => (sbyte)x);
        public static implicit operator byte(Value value) => (byte)value.Number;
        public static implicit operator byte[](Value value) => value._array.ConvertAll(x => (byte)x).ToArray();
        public static implicit operator List<byte>(Value value) => value._array.ConvertAll(x => (byte)x);
        
        public static implicit operator Uri(Value value) => value.Uri;
        public static implicit operator Uri[](Value value) => value._array.ConvertAll(x => (Uri)x).ToArray();
        public static implicit operator List<Uri>(Value value) => value._array.ConvertAll(x => (Uri)x);
        public static implicit operator Guid(Value value) => value.Guid;
        public static implicit operator Guid[](Value value) => value._array.ConvertAll(x => (Guid)x).ToArray();
        public static implicit operator List<Guid>(Value value) => value._array.ConvertAll(x => (Guid)x);
        public static implicit operator DateTime(Value value) => value.DateTime;
        public static implicit operator DateTime[](Value value) => value._array.ConvertAll(x => (DateTime)x).ToArray();
        public static implicit operator List<DateTime>(Value value) => value._array.ConvertAll(x => (DateTime)x);
        public static implicit operator DateTimeOffset(Value value) => value.DateTimeOffset;
        public static implicit operator DateTimeOffset[](Value value) => value._array.ConvertAll(x => (DateTimeOffset)x).ToArray();
        public static implicit operator List<DateTimeOffset>(Value value) => value._array.ConvertAll(x => (DateTimeOffset)x);
        public static implicit operator TimeSpan(Value value) => value.TimeSpan;
        public static implicit operator TimeSpan[](Value value) => value._array.ConvertAll(x => (TimeSpan)x).ToArray();
        public static implicit operator List<TimeSpan>(Value value) => value._array.ConvertAll(x => (TimeSpan)x);
        public static implicit operator Color(Value value) => value.Color;
        public static implicit operator Color[](Value value) => value._array.ConvertAll(x => (Color)x).ToArray();
        public static implicit operator List<Color>(Value value) => value._array.ConvertAll(x => (Color)x);
        public static implicit operator Color32(Value value) => value.Color32;
        public static implicit operator Color32[](Value value) => value._array.ConvertAll(x => (Color32)x).ToArray();
        public static implicit operator List<Color32>(Value value) => value._array.ConvertAll(x => (Color32)x);
        public static implicit operator Vector2(Value value) => value.Vector2;
        public static implicit operator Vector2[](Value value) => value._array.ConvertAll(x => (Vector2)x).ToArray();
        public static implicit operator List<Vector2>(Value value) => value._array.ConvertAll(x => (Vector2)x);
        public static implicit operator Vector3(Value value) => value.Vector3;
        public static implicit operator Vector3[](Value value) => value._array.ConvertAll(x => (Vector3)x).ToArray();
        public static implicit operator List<Vector3>(Value value) => value._array.ConvertAll(x => (Vector3)x);
        public static implicit operator Vector4(Value value) => value.Vector4;
        public static implicit operator Vector4[](Value value) => value._array.ConvertAll(x => (Vector4)x).ToArray();
        public static implicit operator List<Vector4>(Value value) => value._array.ConvertAll(x => (Vector4)x);
        public static implicit operator Quaternion(Value value) => value.Quaternion;
        public static implicit operator Quaternion[](Value value) => value._array.ConvertAll(x => (Quaternion)x).ToArray();
        public static implicit operator List<Quaternion>(Value value) => value._array.ConvertAll(x => (Quaternion)x);
        public static implicit operator Bounds(Value value) => value.Bounds;
        public static implicit operator Bounds[](Value value) => value._array.ConvertAll(x => (Bounds)x).ToArray();
        public static implicit operator List<Bounds>(Value value) => value._array.ConvertAll(x => (Bounds)x);
        public static implicit operator Rect(Value value) => value.Rect;
        public static implicit operator Rect[](Value value) => value._array.ConvertAll(x => (Rect)x).ToArray();
        public static implicit operator List<Rect>(Value value) => value._array.ConvertAll(x => (Rect)x);
        
        #endregion

        #region Serialization

        #region Writer

        private void Write(StringWriter writer, bool includeTypeInfo = false)
        {
            if (includeTypeInfo)
            {
                writer.Write("{");
                writer.Write($"\"JsonType\": \"{_valueType}\", \"DataType\": \"{_dataType}\", \"Value\": ");
            }
            switch (_valueType)
            {
                case ValueTypes.UNDEFINED:
                case ValueTypes.NULL:
                    writer.Write("null");
                    break;
                case ValueTypes.STRING:
                    writer.Write("\"");
                    writer.Write(_string);
                    writer.Write("\"");
                    break;
                case ValueTypes.BOOLEAN:
                    writer.Write(_bool ? "true" : "false");
                    break;
                case ValueTypes.NUMBER:
                    writer.Write(_number);
                    break;
                case ValueTypes.ARRAY:
                    writer.Write("[");
                    int arrayIndex = 0;
                    int arrayCount = _array.Count - 1;
                    foreach (Value item in _array)
                    {
                        item.Write(writer, includeTypeInfo);
                        if (arrayIndex < arrayCount) writer.Write(", ");
                        arrayIndex++;
                    }
                    writer.Write("]");
                    break;
                case ValueTypes.OBJECT:
                    writer.Write("{");
                    if (_dataType == DataTypes.CONTAINER)
                    {
                        int containerIndex = 0;
                        int containerCount = _container.Count - 1;
                        foreach (KeyValuePair<string, Value> kvp in _container)
                        {
                            writer.Write($"\"{kvp.Key}\": ");
                            kvp.Value.Write(writer, includeTypeInfo);
                            if (containerIndex < containerCount) writer.Write(", ");
                            containerIndex++;
                        }
                    }
                    else
                    {
                        int containerIndex = 0;
                        int containerCount = _container.Count - 1;
                        foreach (KeyValuePair<string, Value> kvp in _container)
                        {
                            writer.Write($"\"{kvp.Key}\": {kvp.Value}");
                            if (containerIndex < containerCount) writer.Write(", ");
                            containerIndex++;
                        }
                    }
                    writer.Write("}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (includeTypeInfo)
            {
                writer.Write("}");
            }
        }
        
        #endregion
        
        #region Reader

        private static Value FromSerialization(Value valueType, Value dataType, Value value)
        {
            value._valueType = (ValueTypes) Enum.Parse(typeof(ValueTypes), valueType);
            value._dataType = (DataTypes) Enum.Parse(typeof(DataTypes), dataType);
            return value;
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
            double.TryParse(number, out double parsedDouble);
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
                    case Token.CURLY_CLOSE when data.ContainsKey("JsonType") && data.ContainsKey("DataType"):
                        return FromSerialization(data["JsonType"], data["DataType"], data["Value"]);
                    case Token.CURLY_CLOSE:
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

        #endregion

        public string Serialize()
        {
            StringWriter writer = new StringWriter();
            Write(writer, true);
            return writer.ToString();
        }
        
        public static Value Deserialize(string json)
        {
            return ParseValue(new StringReader(json));
        }
        
        #endregion
    }
}