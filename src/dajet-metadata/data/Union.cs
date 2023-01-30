using System;
using System.Text.Json.Serialization;

namespace DaJet.Data
{
    public enum UnionTag : byte
    {
        Undefined = 0x01, // _TYPE
        Boolean   = 0x02, // _L
        Numeric   = 0x03, // _N
        DateTime  = 0x04, // _T
        String    = 0x05, // _S
        Binary    = 0x06, // _B
        Unknown   = 0x07, // ???
        Entity    = 0x08  // [_TRef] _RRef
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
        private readonly UnionTag _tag;
        public static readonly Union Undefined = new CaseUndefined();
        public static Type GetType(UnionTag tag)
        {
            if (tag == UnionTag.Boolean)
            {
                return typeof(bool);
            }
            else if (tag == UnionTag.Numeric)
            {
                return typeof(decimal);
            }
            else if (tag == UnionTag.DateTime)
            {
                return typeof(DateTime);
            }
            else if (tag == UnionTag.String)
            {
                return typeof(string);
            }
            else if (tag == UnionTag.Binary)
            {
                return typeof(byte[]);
            }
            else if (tag == UnionTag.Entity)
            {
                return typeof(Entity);
            }

            return null; // UnionTag.Undefined
        }
        protected Union(UnionTag tag) { _tag = tag; }
        public UnionTag Tag { get { return _tag; } }
        [JsonIgnore] public bool IsUndefined { get { return _tag == UnionTag.Undefined; } }
        public abstract object Value { get; }
        public abstract bool GetBoolean();
        public abstract decimal GetNumeric();
        public abstract DateTime GetDateTime();
        public abstract string GetString();
        public abstract byte[] GetBinary();
        public abstract Entity GetEntity();
        public override string ToString()
        {
            return IsUndefined ? "Неопределено" : (Value == null ? "NULL" : Value.ToString()!);
        }
        public static implicit operator Union(bool value) => new CaseBoolean(value);
        public static implicit operator Union(decimal value) => new CaseNumeric(value);
        public static implicit operator Union(DateTime value) => new CaseDateTime(value);
        public static implicit operator Union(string value) => new CaseString(value);
        public static implicit operator Union(byte[] value) => new CaseBinary(value);
        public static implicit operator Union(Entity value) => new CaseEntity(value);
        public sealed class CaseUndefined : Union
        {
            public CaseUndefined() : base(UnionTag.Undefined) { }
            public override object Value { get { return null!; } }
            public override bool GetBoolean()
            {
                throw new BadUnionAccessException(typeof(bool), typeof(CaseUndefined));
            }
            public override decimal GetNumeric()
            {
                throw new BadUnionAccessException(typeof(decimal), typeof(CaseUndefined));
            }
            public override DateTime GetDateTime()
            {
                throw new BadUnionAccessException(typeof(DateTime), typeof(CaseUndefined));
            }
            public override string GetString()
            {
                throw new BadUnionAccessException(typeof(string), typeof(CaseUndefined));
            }
            public override byte[] GetBinary()
            {
                throw new BadUnionAccessException(typeof(byte[]), typeof(CaseUndefined));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseUndefined));
            }
        }
        public sealed class CaseBoolean : Union
        {
            private readonly bool _value;
            public CaseBoolean(bool value) : base(UnionTag.Boolean) { _value = value; }
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
            public override byte[] GetBinary()
            {
                throw new BadUnionAccessException(typeof(byte[]), typeof(CaseBoolean));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseBoolean));
            }
        }
        public sealed class CaseNumeric : Union
        {
            private readonly decimal _value;
            public CaseNumeric(decimal value) : base(UnionTag.Numeric) { _value = value; }
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
            public override byte[] GetBinary()
            {
                throw new BadUnionAccessException(typeof(byte[]), typeof(CaseNumeric));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseNumeric));
            }
        }
        public sealed class CaseDateTime : Union
        {
            private readonly DateTime _value;
            public CaseDateTime(DateTime value) : base(UnionTag.DateTime) { _value = value; }
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
            public override byte[] GetBinary()
            {
                throw new BadUnionAccessException(typeof(byte[]), typeof(CaseDateTime));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseDateTime));
            }
        }
        public sealed class CaseString : Union
        {
            private readonly string _value;
            public CaseString(string value) : base(UnionTag.String) { _value = value; }
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
            public override byte[] GetBinary()
            {
                throw new BadUnionAccessException(typeof(byte[]), typeof(CaseString));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseString));
            }
        }
        public sealed class CaseBinary : Union
        {
            private readonly byte[] _value;
            public CaseBinary(byte[] value) : base(UnionTag.Binary) { _value = value; }
            public override object Value { get { return _value; } }
            public override bool GetBoolean()
            {
                throw new BadUnionAccessException(typeof(bool), typeof(CaseBinary));
            }
            public override decimal GetNumeric()
            {
                throw new BadUnionAccessException(typeof(decimal), typeof(CaseBinary));
            }
            public override DateTime GetDateTime()
            {
                throw new BadUnionAccessException(typeof(DateTime), typeof(CaseBinary));
            }
            public override string GetString()
            {
                throw new BadUnionAccessException(typeof(byte[]), typeof(CaseBinary)); ;
            }
            public override byte[] GetBinary()
            {
                return _value;
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseBinary));
            }
        }
        public sealed class CaseEntity : Union
        {
            private readonly Entity _value;
            public CaseEntity(Entity value) : base(UnionTag.Entity) { _value = value; }
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
            public override byte[] GetBinary()
            {
                throw new BadUnionAccessException(typeof(byte[]), typeof(CaseEntity));
            }
            public override Entity GetEntity()
            {
                return _value;
            }
        }
    }
}