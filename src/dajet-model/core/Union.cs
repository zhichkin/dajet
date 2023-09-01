using System;
using System.Text.Json.Serialization;

namespace DaJet
{
    public enum UnionTag : byte
    {
        TypeCode  = 0xFF, // TRef entity type code database column
        Undefined = 0x00,
        Tag       = 0x01, // TYPE
        Boolean   = 0x02, // L
        Numeric   = 0x03, // N
        DateTime  = 0x04, // T
        String    = 0x05, // S
        Binary    = 0x06, // B
        Uuid      = 0x07, // U
        Entity    = 0x08, // # [_TRef] _RRef
        Version   = 0x09, // V database version byte[8]
        Integer   = 0x0A  // I
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
        protected Union(UnionTag tag) { _tag = tag; }
        [JsonIgnore] public bool IsUndefined { get { return _tag == UnionTag.Undefined; } }
        public UnionTag Tag { get { return _tag; } } // TYPE
        public abstract object Value { get; }
        public abstract Union Copy();
        public abstract bool GetBoolean(); // L
        public abstract decimal GetNumeric(); // N
        public abstract DateTime GetDateTime(); // T
        public abstract string GetString(); // S
        public abstract byte[] GetBinary(); // B
        public abstract Guid GetUuid(); // U
        public abstract Entity GetEntity(); // #
        public abstract ulong GetVersion(); // V
        public abstract int GetInteger(); // I
        public override string ToString()
        {
            return IsUndefined ? "Неопределено" : (Value == null ? "NULL" : Value.ToString());
        }
        public static implicit operator Union(bool value) => new CaseBoolean(value);
        public static implicit operator Union(decimal value) => new CaseNumeric(value);
        public static implicit operator Union(DateTime value) => new CaseDateTime(value);
        public static implicit operator Union(string value) => new CaseString(value);
        public static implicit operator Union(byte[] value) => new CaseBinary(value);
        public static implicit operator Union(Guid value) => new CaseUuid(value);
        public static implicit operator Union(Entity value) => new CaseEntity(value);
        public static implicit operator Union(ulong value) => new CaseVersion(value);
        public static implicit operator Union(int value) => new CaseInteger(value);
        public sealed class CaseUndefined : Union
        {
            public CaseUndefined() : base(UnionTag.Undefined) { }
            public override object Value { get { return null!; } }
            public override Union Copy() { return new CaseUndefined(); }
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
            public override Guid GetUuid()
            {
                throw new BadUnionAccessException(typeof(Guid), typeof(CaseUndefined));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseUndefined));
            }
            public override ulong GetVersion()
            {
                throw new BadUnionAccessException(typeof(ulong), typeof(CaseUndefined));
            }
            public override int GetInteger()
            {
                throw new BadUnionAccessException(typeof(int), typeof(CaseUndefined));
            }
        }
        public sealed class CaseBoolean : Union
        {
            private readonly bool _value;
            public CaseBoolean(bool value) : base(UnionTag.Boolean) { _value = value; }
            public override object Value { get { return _value; } }
            public override Union Copy() { return new CaseBoolean(_value); }
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
            public override Guid GetUuid()
            {
                throw new BadUnionAccessException(typeof(Guid), typeof(CaseBoolean));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseBoolean));
            }
            public override ulong GetVersion()
            {
                throw new BadUnionAccessException(typeof(ulong), typeof(CaseBoolean));
            }
            public override int GetInteger()
            {
                throw new BadUnionAccessException(typeof(int), typeof(CaseBoolean));
            }
        }
        public sealed class CaseNumeric : Union
        {
            private readonly decimal _value;
            public CaseNumeric(decimal value) : base(UnionTag.Numeric) { _value = value; }
            public override object Value { get { return _value; } }
            public override Union Copy() { return new CaseNumeric(_value); }
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
            public override Guid GetUuid()
            {
                throw new BadUnionAccessException(typeof(Guid), typeof(CaseNumeric));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseNumeric));
            }
            public override ulong GetVersion()
            {
                throw new BadUnionAccessException(typeof(ulong), typeof(CaseNumeric));
            }
            public override int GetInteger()
            {
                throw new BadUnionAccessException(typeof(int), typeof(CaseInteger));
            }
        }
        public sealed class CaseDateTime : Union
        {
            private readonly DateTime _value;
            public CaseDateTime(DateTime value) : base(UnionTag.DateTime) { _value = value; }
            public override object Value { get { return _value; } }
            public override Union Copy() { return new CaseDateTime(_value); }
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
            public override Guid GetUuid()
            {
                throw new BadUnionAccessException(typeof(Guid), typeof(CaseDateTime));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseDateTime));
            }
            public override ulong GetVersion()
            {
                throw new BadUnionAccessException(typeof(ulong), typeof(CaseDateTime));
            }
            public override int GetInteger()
            {
                throw new BadUnionAccessException(typeof(int), typeof(CaseDateTime));
            }
        }
        public sealed class CaseString : Union
        {
            private readonly string _value;
            public CaseString(string value) : base(UnionTag.String) { _value = value; }
            public override object Value { get { return _value; } }
            public override Union Copy() { return new CaseString(_value); } //TODO: make copy of string ?
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
            public override Guid GetUuid()
            {
                throw new BadUnionAccessException(typeof(Guid), typeof(CaseString));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseString));
            }
            public override ulong GetVersion()
            {
                throw new BadUnionAccessException(typeof(ulong), typeof(CaseString));
            }
            public override int GetInteger()
            {
                throw new BadUnionAccessException(typeof(int), typeof(CaseString));
            }
        }
        public sealed class CaseBinary : Union
        {
            private readonly byte[] _value;
            public CaseBinary(byte[] value) : base(UnionTag.Binary) { _value = value; }
            public override object Value { get { return _value; } }
            public override Union Copy() { return new CaseBinary(_value); } //TODO: make copy of byte[] ?
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
                throw new BadUnionAccessException(typeof(string), typeof(CaseBinary)); ;
            }
            public override byte[] GetBinary()
            {
                return _value;
            }
            public override Guid GetUuid()
            {
                throw new BadUnionAccessException(typeof(Guid), typeof(CaseBinary));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseBinary));
            }
            public override ulong GetVersion()
            {
                throw new BadUnionAccessException(typeof(ulong), typeof(CaseBinary));
            }
            public override int GetInteger()
            {
                throw new BadUnionAccessException(typeof(int), typeof(CaseBinary));
            }
        }
        public sealed class CaseUuid : Union
        {
            private readonly Guid _value;
            public CaseUuid(Guid value) : base(UnionTag.Uuid) { _value = value; }
            public override object Value { get { return _value; } }
            public override Union Copy() { return new CaseUuid(_value); }
            public override bool GetBoolean()
            {
                throw new BadUnionAccessException(typeof(bool), typeof(CaseUuid));
            }
            public override decimal GetNumeric()
            {
                throw new BadUnionAccessException(typeof(decimal), typeof(CaseUuid));
            }
            public override DateTime GetDateTime()
            {
                throw new BadUnionAccessException(typeof(DateTime), typeof(CaseUuid));
            }
            public override string GetString()
            {
                throw new BadUnionAccessException(typeof(string), typeof(CaseUuid)); ;
            }
            public override byte[] GetBinary()
            {
                throw new BadUnionAccessException(typeof(byte[]), typeof(CaseUuid));
            }
            public override Guid GetUuid()
            {
                return _value;
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseUuid));
            }
            public override ulong GetVersion()
            {
                throw new BadUnionAccessException(typeof(ulong), typeof(CaseUuid));
            }
            public override int GetInteger()
            {
                throw new BadUnionAccessException(typeof(int), typeof(CaseUuid));
            }
        }
        public sealed class CaseEntity : Union
        {
            private readonly Entity _value;
            public CaseEntity(Entity value) : base(UnionTag.Entity) { _value = value; }
            public override object Value { get { return _value; } }
            public override Union Copy() { return new CaseEntity(_value.Copy()); }
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
            public override Guid GetUuid()
            {
                throw new BadUnionAccessException(typeof(Guid), typeof(CaseEntity));
            }
            public override Entity GetEntity()
            {
                return _value;
            }
            public override ulong GetVersion()
            {
                throw new BadUnionAccessException(typeof(ulong), typeof(CaseEntity));
            }
            public override int GetInteger()
            {
                throw new BadUnionAccessException(typeof(int), typeof(CaseEntity));
            }
        }
        public sealed class CaseVersion : Union
        {
            private readonly ulong _value;
            public CaseVersion(ulong value) : base(UnionTag.Version) { _value = value; }
            public override object Value { get { return _value; } }
            public override Union Copy() { return new CaseVersion(_value); }
            public override bool GetBoolean()
            {
                throw new BadUnionAccessException(typeof(bool), typeof(CaseVersion));
            }
            public override decimal GetNumeric()
            {
                throw new BadUnionAccessException(typeof(decimal), typeof(CaseVersion));
            }
            public override DateTime GetDateTime()
            {
                throw new BadUnionAccessException(typeof(DateTime), typeof(CaseVersion));
            }
            public override string GetString()
            {
                throw new BadUnionAccessException(typeof(string), typeof(CaseVersion));
            }
            public override byte[] GetBinary()
            {
                throw new BadUnionAccessException(typeof(byte[]), typeof(CaseVersion));
            }
            public override Guid GetUuid()
            {
                throw new BadUnionAccessException(typeof(Guid), typeof(CaseVersion));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseVersion));
            }
            public override ulong GetVersion()
            {
                return _value;
            }
            public override int GetInteger()
            {
                throw new BadUnionAccessException(typeof(int), typeof(CaseVersion));
            }
        }
        public sealed class CaseInteger : Union
        {
            private readonly int _value;
            public CaseInteger(int value) : base(UnionTag.Integer) { _value = value; }
            public override object Value { get { return _value; } }
            public override Union Copy() { return new CaseInteger(_value); }
            public override bool GetBoolean()
            {
                throw new BadUnionAccessException(typeof(bool), typeof(CaseInteger));
            }
            public override decimal GetNumeric()
            {
                throw new BadUnionAccessException(typeof(decimal), typeof(CaseInteger));
            }
            public override DateTime GetDateTime()
            {
                throw new BadUnionAccessException(typeof(DateTime), typeof(CaseInteger));
            }
            public override string GetString()
            {
                throw new BadUnionAccessException(typeof(string), typeof(CaseInteger));
            }
            public override byte[] GetBinary()
            {
                throw new BadUnionAccessException(typeof(byte[]), typeof(CaseInteger));
            }
            public override Guid GetUuid()
            {
                throw new BadUnionAccessException(typeof(Guid), typeof(CaseInteger));
            }
            public override Entity GetEntity()
            {
                throw new BadUnionAccessException(typeof(Entity), typeof(CaseInteger));
            }
            public override ulong GetVersion()
            {
                throw new BadUnionAccessException(typeof(ulong), typeof(CaseInteger));
            }
            public override int GetInteger()
            {
                return _value;
            }
        }
    }
}