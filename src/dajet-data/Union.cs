namespace DaJet.Data
{
    public enum UnionTags : byte
    {
        // TODO: byte[] Binary _B 0x06 ???
        Empty    = 0x01, // _TYPE
        Boolean  = 0x02, // _L
        Numeric  = 0x03, // _N
        DateTime = 0x04, // _T
        String   = 0x05, // _S
        Entity   = 0x08  // [_TRef] _RRef
    }
    public sealed class BadUnionAccessException : Exception
    {
        public BadUnionAccessException(Type value, Type union) : base($"Bad union access [{value}] {union}") { }
    }
    public sealed class BadUnionAssignmentException : Exception
    {
        public BadUnionAssignmentException() : base() { }
        public BadUnionAssignmentException(Type value, Type union) : base($"Bad union assignment [{value}] {union}") { }
    }
    public abstract class Union
    {
        private readonly UnionTags _tag;
        public static readonly Union Empty = new CaseEmpty();
        protected Union(UnionTags tag) { _tag = tag; }
        public UnionTags Tag { get { return _tag; } }
        public bool IsEmpty { get { return _tag == UnionTags.Empty; } }
        public abstract object Value { get; }
        public abstract bool GetBoolean();
        public abstract decimal GetNumeric();
        public abstract DateTime GetDateTime();
        public abstract string GetString();
        public abstract EntityRef GetEntityRef();
        public override string ToString()
        {
            return IsEmpty ? "Неопределено" : (Value == null ? "NULL" : Value.ToString()!);
        }
        public static implicit operator Union(bool value) => new CaseBoolean(value);
        public static implicit operator Union(decimal value) => new CaseNumeric(value);
        public static implicit operator Union(DateTime value) => new CaseDateTime(value);
        public static implicit operator Union(string value) => new CaseString(value);
        public static implicit operator Union(EntityRef value) => new CaseEntity(value);
        public sealed class CaseEmpty : Union
        {
            public CaseEmpty() : base(UnionTags.Empty) { }
            public override object Value { get { return null!; } }
            public override bool GetBoolean()
            {
                throw new BadUnionAccessException(typeof(bool), typeof(CaseEmpty));
            }
            public override decimal GetNumeric()
            {
                throw new BadUnionAccessException(typeof(decimal), typeof(CaseEmpty));
            }
            public override DateTime GetDateTime()
            {
                throw new BadUnionAccessException(typeof(DateTime), typeof(CaseEmpty));
            }
            public override string GetString()
            {
                throw new BadUnionAccessException(typeof(string), typeof(CaseEmpty));
            }
            public override EntityRef GetEntityRef()
            {
                throw new BadUnionAccessException(typeof(EntityRef), typeof(CaseEmpty));
            }
        }
        public sealed class CaseBoolean : Union
        {
            private readonly bool _value;
            public CaseBoolean(bool value) : base(UnionTags.Boolean) { _value = value; }
            public override object Value { get { return _value; } }
            public override bool GetBoolean()
            {
                return _value;
            }
            public override decimal GetNumeric()
            {
                throw new BadUnionAccessException(typeof(decimal), typeof(CaseBoolean));
            }
            public override DateTime GetDateTime()
            {
                throw new BadUnionAccessException(typeof(DateTime), typeof(CaseBoolean));
            }
            public override string GetString()
            {
                throw new BadUnionAccessException(typeof(string), typeof(CaseBoolean));
            }
            public override EntityRef GetEntityRef()
            {
                throw new BadUnionAccessException(typeof(EntityRef), typeof(CaseBoolean));
            }
        }
        public sealed class CaseNumeric : Union
        {
            private readonly decimal _value;
            public CaseNumeric(decimal value) : base(UnionTags.Numeric) { _value = value; }
            public override object Value { get { return _value; } }
            public override bool GetBoolean()
            {
                throw new BadUnionAccessException(typeof(bool), typeof(CaseNumeric));
            }
            public override decimal GetNumeric()
            {
                return _value;
            }
            public override DateTime GetDateTime()
            {
                throw new BadUnionAccessException(typeof(DateTime), typeof(CaseNumeric));
            }
            public override string GetString()
            {
                throw new BadUnionAccessException(typeof(string), typeof(CaseNumeric));
            }
            public override EntityRef GetEntityRef()
            {
                throw new BadUnionAccessException(typeof(EntityRef), typeof(CaseNumeric));
            }
        }
        public sealed class CaseDateTime : Union
        {
            private readonly DateTime _value;
            public CaseDateTime(DateTime value) : base(UnionTags.DateTime) { _value = value; }
            public override object Value { get { return _value; } }
            public override bool GetBoolean()
            {
                throw new BadUnionAccessException(typeof(bool), typeof(CaseDateTime));
            }
            public override decimal GetNumeric()
            {
                throw new BadUnionAccessException(typeof(decimal), typeof(CaseDateTime));
            }
            public override DateTime GetDateTime()
            {
                return _value;
            }
            public override string GetString()
            {
                throw new BadUnionAccessException(typeof(string), typeof(CaseDateTime));
            }
            public override EntityRef GetEntityRef()
            {
                throw new BadUnionAccessException(typeof(EntityRef), typeof(CaseDateTime));
            }
        }
        public sealed class CaseString : Union
        {
            private readonly string _value;
            public CaseString(string value) : base(UnionTags.String) { _value = value; }
            public override object Value { get { return _value; } }
            public override bool GetBoolean()
            {
                throw new BadUnionAccessException(typeof(bool), typeof(CaseString));
            }
            public override decimal GetNumeric()
            {
                throw new BadUnionAccessException(typeof(decimal), typeof(CaseString));
            }
            public override DateTime GetDateTime()
            {
                throw new BadUnionAccessException(typeof(DateTime), typeof(CaseString));
            }
            public override string GetString()
            {
                return _value;
            }
            public override EntityRef GetEntityRef()
            {
                throw new BadUnionAccessException(typeof(EntityRef), typeof(CaseString));
            }
        }
        public sealed class CaseEntity : Union
        {
            private readonly EntityRef _value;
            public CaseEntity(EntityRef value) : base(UnionTags.Entity) { _value = value; }
            public override object Value { get { return _value; } }
            public override bool GetBoolean()
            {
                throw new BadUnionAccessException(typeof(bool), typeof(CaseEntity));
            }
            public override decimal GetNumeric()
            {
                throw new BadUnionAccessException(typeof(decimal), typeof(CaseEntity));
            }
            public override DateTime GetDateTime()
            {
                throw new BadUnionAccessException(typeof(DateTime), typeof(CaseEntity));
            }
            public override string GetString()
            {
                throw new BadUnionAccessException(typeof(string), typeof(CaseEntity));
            }
            public override EntityRef GetEntityRef()
            {
                return _value;
            }
        }
    }
}