namespace DaJet.Data
{
    public abstract class TypeUnion
    {
        private readonly int _tag;
        protected TypeUnion(int tag) { _tag = tag; }
        public int Tag { get { return _tag; } }
        public abstract object Value { get; }
        public abstract bool GetBoolean();
        public abstract decimal GetNumeric();
        public abstract DateTime GetDateTime();
        public abstract string GetString();
        public abstract EntityRef GetEntityRef();

        public static implicit operator TypeUnion(bool value) => new CaseBoolean(value);
        public static implicit operator TypeUnion(decimal value) => new CaseNumeric(value);
        public static implicit operator TypeUnion(DateTime value) => new CaseDateTime(value);
        public static implicit operator TypeUnion(string value) => new CaseString(value);
        public static implicit operator TypeUnion(EntityRef value) => new CaseEntityRef(value);
        public sealed class CaseBoolean : TypeUnion
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
        public sealed class CaseNumeric : TypeUnion
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
        public sealed class CaseDateTime : TypeUnion
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
        public sealed class CaseString : TypeUnion
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
        public sealed class CaseEntityRef : TypeUnion
        {
            private readonly EntityRef _value;
            public CaseEntityRef(EntityRef value) : base(UnionTags.Entity) { _value = value; }
            public override object Value { get { return _value; } }
            public override bool GetBoolean()
            {
                throw new BadUnionAccessException(typeof(bool), typeof(CaseEntityRef));
            }
            public override decimal GetNumeric()
            {
                throw new BadUnionAccessException(typeof(decimal), typeof(CaseEntityRef));
            }
            public override DateTime GetDateTime()
            {
                throw new BadUnionAccessException(typeof(DateTime), typeof(CaseEntityRef));
            }
            public override string GetString()
            {
                throw new BadUnionAccessException(typeof(string), typeof(CaseEntityRef));
            }
            public override EntityRef GetEntityRef()
            {
                return _value;
            }
        }
    }
}