using System.Runtime.InteropServices;

namespace DaJet.Data
{
    public static class UnionTags
    {
        // TODO: byte[] Binary _B 0x06 !!!
        public static readonly int Empty    = 0x01; // _TYPE
        public static readonly int Boolean  = 0x02; // _L
        public static readonly int Numeric  = 0x03; // _N
        public static readonly int DateTime = 0x04; // _T
        public static readonly int String   = 0x05; // _S
        public static readonly int Entity   = 0x08; // [_TRef] _RRef
        public static readonly Dictionary<Type, int> Map = new()
        {
            { typeof(bool),      UnionTags.Boolean  },
            { typeof(decimal),   UnionTags.Numeric  },
            { typeof(DateTime),  UnionTags.DateTime },
            { typeof(string),    UnionTags.String   },
            { typeof(EntityRef), UnionTags.Entity   }
        };
        public static int Resolve(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            if (Map.TryGetValue(type, out int tag))
            {
                return tag;
            }

            return UnionTags.Empty;
        }
        public static Type Resolve(int tag)
        {
            if      (tag == UnionTags.Empty)    { return null!;             }
            else if (tag == UnionTags.Boolean)  { return typeof(bool);      }
            else if (tag == UnionTags.Numeric)  { return typeof(decimal);   }
            else if (tag == UnionTags.DateTime) { return typeof(DateTime);  }
            else if (tag == UnionTags.String)   { return typeof(string);    }
            else if (tag == UnionTags.Entity)   { return typeof(EntityRef); }
            
            throw new InvalidOperationException($"Unknown tag value: {tag}");
        }
    }

    #region "CLASS TAGGED UNION"

    public sealed class BadUnionAccessException : Exception
    {
        public BadUnionAccessException(Type value, Type union) : base($"Bad union access [{value}] {union}") { }
    }
    public sealed class BadUnionAssignmentException : Exception
    {
        public BadUnionAssignmentException(Type value, Type union) : base($"Bad union assignment [{value}] {union}") { }
    }

    public abstract class Union
    {
        protected readonly int _tag;
        public int Tag { get { return _tag; } }
        protected Union(Type type)
        {
            _tag = UnionTags.Resolve(type);

            if (_tag == UnionTags.Empty)
            {
                throw new BadUnionAssignmentException(type, GetType());
            }
        }
        public abstract object Value { get; }
        public abstract bool TryGet(out bool value);
        public abstract bool TryGet(out decimal value);
        public abstract bool TryGet(out DateTime value);
        public abstract bool TryGet(out string value);
        public abstract bool TryGet(out EntityRef value);
        protected bool TryGet<T>(in T value, out bool result)
        {
            if (_tag == UnionTags.Boolean && value is bool test)
            {
                result = test;
                return true;
            }
            
            result = default;
            return false;
        }
        protected bool TryGet<T>(in T value, out decimal result)
        {
            if (_tag == UnionTags.Numeric && value is decimal test)
            {
                result = test;
                return true;
            }

            result = default;
            return false;
        }
        protected bool TryGet<T>(in T value, out DateTime result)
        {
            if (_tag == UnionTags.DateTime && value is DateTime test)
            {
                result = test;
                return true;
            }

            result = default;
            return false;
        }
        protected bool TryGet<T>(in T value, out string result)
        {
            if (_tag == UnionTags.String && value is string test)
            {
                result = test;
                return true;
            }

            result = default!;
            return false;
        }
        protected bool TryGet<T>(in T value, out EntityRef result)
        {
            if (_tag == UnionTags.Entity && value is EntityRef test)
            {
                result = test;
                return true;
            }

            result = default!;
            return false;
        }
    }
    public abstract class Union<T0, T1> : Union
    {
        protected Union(Type type) : base(type) { }
        public virtual void Get(out T0 value) { throw new BadUnionAccessException(typeof(T0), GetType()); }
        public virtual void Get(out T1 value) { throw new BadUnionAccessException(typeof(T1), GetType()); }

        public static implicit operator Union<T0, T1>(T0 value) => new Case0<T0>(value);
        public static implicit operator Union<T0, T1>(T1 value) => new Case1<T1>(value);
        public sealed class Case0<T> : Union<T0, T1> where T : T0
        {
            private readonly T _value;
            public Case0(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T0 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
        public sealed class Case1<T> : Union<T0, T1> where T : T1
        {
            private readonly T _value;
            public Case1(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T1 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
    }
    public abstract class Union<T0, T1, T2> : Union
    {
        protected Union(Type type) : base(type) { }
        public virtual void Get(out T0 value) { throw new BadUnionAccessException(typeof(T0), GetType()); }
        public virtual void Get(out T1 value) { throw new BadUnionAccessException(typeof(T1), GetType()); }
        public virtual void Get(out T2 value) { throw new BadUnionAccessException(typeof(T2), GetType()); }

        public static implicit operator Union<T0, T1, T2>(T0 value) => new Case0<T0>(value);
        public static implicit operator Union<T0, T1, T2>(T1 value) => new Case1<T1>(value);
        public static implicit operator Union<T0, T1, T2>(T2 value) => new Case2<T2>(value);
        public sealed class Case0<T> : Union<T0, T1, T2> where T : T0
        {
            private readonly T _value;
            public Case0(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T0 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
        public sealed class Case1<T> : Union<T0, T1, T2> where T : T1
        {
            private readonly T _value;
            public Case1(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T1 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
        public sealed class Case2<T> : Union<T0, T1, T2> where T : T2
        {
            private readonly T _value;
            public Case2(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T2 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
    }
    public abstract class Union<T0, T1, T2, T3> : Union
    {
        protected Union(Type type) : base(type) { }
        public virtual void Get(out T0 value) { throw new BadUnionAccessException(typeof(T0), GetType()); }
        public virtual void Get(out T1 value) { throw new BadUnionAccessException(typeof(T1), GetType()); }
        public virtual void Get(out T2 value) { throw new BadUnionAccessException(typeof(T2), GetType()); }
        public virtual void Get(out T3 value) { throw new BadUnionAccessException(typeof(T3), GetType()); }

        public static implicit operator Union<T0, T1, T2, T3>(T0 value) => new Case0<T0>(value);
        public static implicit operator Union<T0, T1, T2, T3>(T1 value) => new Case1<T1>(value);
        public static implicit operator Union<T0, T1, T2, T3>(T2 value) => new Case2<T2>(value);
        public static implicit operator Union<T0, T1, T2, T3>(T3 value) => new Case3<T3>(value);
        public sealed class Case0<T> : Union<T0, T1, T2, T3> where T : T0
        {
            private readonly T _value;
            public Case0(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T0 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
        public sealed class Case1<T> : Union<T0, T1, T2, T3> where T : T1
        {
            private readonly T _value;
            public Case1(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T1 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
        public sealed class Case2<T> : Union<T0, T1, T2, T3> where T : T2
        {
            private readonly T _value;
            public Case2(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T2 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
        public sealed class Case3<T> : Union<T0, T1, T2, T3> where T : T3
        {
            private readonly T _value;
            public Case3(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T3 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
    }
    public abstract class Union<T0, T1, T2, T3, T4> : Union
    {
        protected Union(Type type) : base(type) { }
        public virtual void Get(out T0 value) { throw new BadUnionAccessException(typeof(T0), GetType()); }
        public virtual void Get(out T1 value) { throw new BadUnionAccessException(typeof(T1), GetType()); }
        public virtual void Get(out T2 value) { throw new BadUnionAccessException(typeof(T2), GetType()); }
        public virtual void Get(out T3 value) { throw new BadUnionAccessException(typeof(T3), GetType()); }
        public virtual void Get(out T4 value) { throw new BadUnionAccessException(typeof(T4), GetType()); }

        public static implicit operator Union<T0, T1, T2, T3, T4>(T0 value) => new Case0<T0>(value);
        public static implicit operator Union<T0, T1, T2, T3, T4>(T1 value) => new Case1<T1>(value);
        public static implicit operator Union<T0, T1, T2, T3, T4>(T2 value) => new Case2<T2>(value);
        public static implicit operator Union<T0, T1, T2, T3, T4>(T3 value) => new Case3<T3>(value);
        public static implicit operator Union<T0, T1, T2, T3, T4>(T4 value) => new Case4<T4>(value);
        public sealed class Case0<T> : Union<T0, T1, T2, T3, T4> where T : T0
        {
            private readonly T _value;
            public Case0(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T0 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
        public sealed class Case1<T> : Union<T0, T1, T2, T3, T4> where T : T1
        {
            private readonly T _value;
            public Case1(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T1 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
        public sealed class Case2<T> : Union<T0, T1, T2, T3, T4> where T : T2
        {
            private readonly T _value;
            public Case2(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T2 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
        public sealed class Case3<T> : Union<T0, T1, T2, T3, T4> where T : T3
        {
            private readonly T _value;
            public Case3(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T3 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
        public sealed class Case4<T> : Union<T0, T1, T2, T3, T4> where T : T4
        {
            private readonly T _value;
            public Case4(T value) : base(typeof(T)) { _value = value; }
            public override object Value { get { return _value!; } }
            public override void Get(out T4 value) { value = _value; }
            public override bool TryGet(out bool value) { return TryGet(in _value, out value); }
            public override bool TryGet(out decimal value) { return TryGet(in _value, out value); }
            public override bool TryGet(out DateTime value) { return TryGet(in _value, out value); }
            public override bool TryGet(out string value) { return TryGet(in _value, out value); }
            public override bool TryGet(out EntityRef value) { return TryGet(in _value, out value); }
        }
    }

    #endregion

    #region "STRUCTURE TAGGED UNION"

    [StructLayout(LayoutKind.Explicit)]
    internal readonly struct HelpUnion
    {
        [FieldOffset(0)] internal readonly bool _boolean = false;
        [FieldOffset(0)] internal readonly decimal _numeric = 0M;
        [FieldOffset(0)] internal readonly DateTime _datetime = DateTime.MinValue;
        [FieldOffset(0)] internal readonly EntityRef _entity = EntityRef.Empty;
        public HelpUnion() { }
        internal HelpUnion(bool value) { _boolean = value; }
        internal HelpUnion(decimal value) { _numeric = value; }
        internal HelpUnion(DateTime value) { _datetime = value; }
        internal HelpUnion(EntityRef value) { _entity = value; }
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct UnionType
    {
        public static readonly UnionType Empty = new();
        
        private readonly int _tag = UnionTags.Empty;
        private readonly HelpUnion _union = new();
        private readonly string _string = string.Empty;

        #region "CONSTRUCTORS"

        public UnionType() { }
        public UnionType(bool value) { _tag = UnionTags.Boolean; _union = new(value); }
        public UnionType(decimal value) { _tag = UnionTags.Numeric; _union = new(value); }
        public UnionType(DateTime value) { _tag = UnionTags.DateTime; _union = new(value); }
        public UnionType(EntityRef value) { _tag = UnionTags.Entity; _union = new(value); }
        public UnionType(string value)
        {
            _tag = value == null
                ? UnionTags.Empty
                : UnionTags.String;

            _string = value ?? string.Empty;
        }

        #endregion

        public int Tag
        {
            get
            {
                // Union u = default; u._tag == 0x00 ¯\_(ツ)_/¯
                return _tag == 0x00 ? UnionTags.Empty : _tag;
            }
        }

        public static implicit operator UnionType(bool value) => new(value);
        public static implicit operator UnionType(decimal value) => new(value);
        public static implicit operator UnionType(DateTime value) => new(value);
        public static implicit operator UnionType(string value) => new(value);
        public static implicit operator UnionType(EntityRef value) => new(value);

        #region "VALUE GETTERS"

        public object GetValue()
        {
            if (_tag == UnionTags.Empty || _tag == 0x00) { return null!; }
            else if (_tag == UnionTags.Boolean) { return _union._boolean; }
            else if (_tag == UnionTags.Numeric) { return _union._numeric; }
            else if (_tag == UnionTags.DateTime) { return _union._datetime; }
            else if (_tag == UnionTags.String) { return _string; }
            return _union._entity;
        }
        public bool TryGetBoolean(out bool value)
        {
            value = _union._boolean;
            return (_tag == UnionTags.Boolean);
        }
        public bool TryGetNumeric(out decimal value)
        {
            value = _union._numeric;
            return (_tag == UnionTags.Numeric);
        }
        public bool TryGetDateTime(out DateTime value)
        {
            value = _union._datetime;
            return (_tag == UnionTags.DateTime);
        }
        public bool TryGetString(out string value)
        {
            value = _string;
            return (_tag == UnionTags.String);
        }
        public bool TryGetEntity(out EntityRef value)
        {
            value = _union._entity;
            return (_tag == UnionTags.Entity);
        }

        #endregion

        public override string ToString()
        {
            if (_tag == UnionTags.Empty || _tag == 0x00) { return "null"; }
            else if (_tag == UnionTags.Boolean) { return _union._boolean.ToString(); }
            else if (_tag == UnionTags.Numeric) { return _union._numeric.ToString(); }
            else if (_tag == UnionTags.DateTime) { return _union._datetime.ToString(); }
            else if (_tag == UnionTags.String) { return (_string ?? string.Empty); }
            return _union._entity.ToString();
        }

        #region " Переопределение методов сравнения "

        public override int GetHashCode()
        {
            if (_tag == UnionTags.String)
            {
                return _string.GetHashCode();
            }
            return _union._entity.GetHashCode();
        }
        public override bool Equals(object? obj)
        {
            if (obj == null) { return false; }

            if (obj is not UnionType test)
            {
                return false;
            }

            return (this == test);
        }
        public static bool operator !=(UnionType left, UnionType right)
        {
            return !(left == right);
        }
        public static bool operator ==(UnionType left, UnionType right)
        {
            if (left.Tag != right.Tag)
            {
                return false;
            }
            else if (left._tag == UnionTags.Empty || left._tag == 0x00)
            {
                return true;
            }
            else if (left._tag == UnionTags.Boolean)
            {
                return left._union._boolean == right._union._boolean;
            }
            else if (left._tag == UnionTags.Numeric)
            {
                return left._union._numeric == right._union._numeric;
            }
            else if (left._tag == UnionTags.DateTime)
            {
                return left._union._datetime == right._union._datetime;
            }
            else if (left._tag == UnionTags.String)
            {
                return left._string == right._string;
            }
            return left._union._entity == right._union._entity;
        }

        #endregion
    }

    #endregion
}