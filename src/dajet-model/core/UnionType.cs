using System;
using System.Collections.Generic;
using System.Text;

namespace DaJet
{
    public sealed class UnionType
    {
        private static Dictionary<string, UnionTag> _literals = new()
        {
            { "boolean",  UnionTag.Boolean },
            { "number",   UnionTag.Numeric },
            { "datetime", UnionTag.DateTime },
            { "string",   UnionTag.String },
            { "binary",   UnionTag.Binary },
            { "uuid",     UnionTag.Uuid },
            { "version",  UnionTag.Version },
            { "integer",  UnionTag.Integer }
        };

        private int _flags = 0;
        private int _typeCode = -1;
        public UnionType() { }
        private void SetBit(int position, bool value)
        {
            if (value) { _flags |= (1 << position); } else { _flags &= ~(1 << position); }
        }
        private bool IsBitSet(int position)
        {
            return (_flags & (1 << position)) == (1 << position);
        }
        public int Flags { get { return _flags; } set { _flags = value; _typeCode = -1; } }
        public UnionType Copy() { return new UnionType() { _flags = _flags, _typeCode = _typeCode }; }
        public bool Is(UnionTag tag) { return IsBitSet((int)tag); }
        public void Add(UnionTag type)
        {
            if (type == UnionTag.TypeCode)
            {
                TypeCode = 0;
                IsEntity = true;
            }
            else
            {
                SetBit((int)type, true);
            }
        }
        public void Add(in Type type)
        {
            if (type == typeof(bool)) { IsBoolean = true; }
            else if (type == typeof(decimal)) { IsNumeric = true; }
            else if (type == typeof(DateTime)) { IsDateTime = true; }
            else if (type == typeof(string)) { IsString = true; }
            else if (type == typeof(byte[])) { IsBinary = true; }
            else if (type == typeof(Guid)) { IsUuid = true; }
            else if (type == typeof(Entity)) { IsEntity = true; }
            else if (type == typeof(ulong)) { IsVersion = true; }
            else if (type == typeof(int)) { IsInteger = true; }
            else if (type == typeof(Union))
            {
                Clear();
                IsBoolean = true;
                IsNumeric = true;
                IsDateTime = true;
                IsString = true;
                TypeCode = 0;
                IsEntity = true;
            }
        }
        public bool ApplySystemType(in string literal, out UnionTag tag)
        {
            if (!_literals.TryGetValue(literal, out tag))
            {
                tag = UnionTag.Entity;
            }
            
            Add(tag);

            return (tag != UnionTag.Entity);
        }
        public void Remove(UnionTag type)
        {
            if (type == UnionTag.Entity)
            {
                IsEntity = false;
                _typeCode = -1;
            }
            else if (type == UnionTag.TypeCode)
            {
                _typeCode = -1;
            }
            else
            {
                SetBit((int)type, false);
            }
        }
        public void Merge(in UnionType union)
        {
            _flags |= union._flags;
            TypeCode = union.TypeCode;
        }
        public void Clear()
        {
            _flags = 0;
            _typeCode = -1;
        }
        public int TypeCode
        {
            get { return _typeCode; }
            set
            {
                if (value == 0) // set multiple type
                {
                    _typeCode = 0;
                }
                else if (value > 0) // set single type
                {
                    if (_typeCode == -1) // was never set before
                    {
                        _typeCode = value;
                    }
                    else if (_typeCode == value)
                    {
                        // skip the same type code
                    }
                    else if (_typeCode > 0) // was set once
                    {
                        _typeCode = 0; // multiple type
                    }
                    else
                    {
                        // ignore - 0 is a final state
                    }
                }
                // -1 value is ignored
            }
        }
        public bool IsUnion
        {
            get
            {
                int count = 0;
                if (IsBoolean) { count++; }  // L
                if (IsNumeric) { count++; }  // N
                if (IsDateTime) { count++; } // T
                if (IsString) { count++; }   // S
                if (IsBinary) { count++; }   // B
                if (IsUuid) { count++; }     // U
                if (IsEntity) { count++; }   // # _TRef + _RRef
                if (IsVersion) { count++; }  // B
                if (IsInteger) { count++; }  // I
                return (count > 1 || IsEntity && TypeCode == 0); // TYPE
            }
        }
        public bool IsUndefined { get { return (_flags == 0); } }
        public bool UseTag { get { return IsBitSet((int)UnionTag.Tag); } set { SetBit((int)UnionTag.Tag, value); } }
        public bool UseTypeCode { get { return TypeCode == 0; } }
        public bool IsBoolean { get { return IsBitSet((int)UnionTag.Boolean); } set { SetBit((int)UnionTag.Boolean, value); } }
        public bool IsNumeric { get { return IsBitSet((int)UnionTag.Numeric); } set { SetBit((int)UnionTag.Numeric, value); } }
        public bool IsDateTime { get { return IsBitSet((int)UnionTag.DateTime); } set { SetBit((int)UnionTag.DateTime, value); } }
        public bool IsString { get { return IsBitSet((int)UnionTag.String); } set { SetBit((int)UnionTag.String, value); } }
        public bool IsBinary { get { return IsBitSet((int)UnionTag.Binary); } set { SetBit((int)UnionTag.Binary, value); } }
        public bool IsUuid { get { return IsBitSet((int)UnionTag.Uuid); } set { SetBit((int)UnionTag.Uuid, value); } }
        public bool IsEntity { get { return IsBitSet((int)UnionTag.Entity); } set { SetBit((int)UnionTag.Entity, value); } }
        public bool IsVersion { get { return IsBitSet((int)UnionTag.Version); } set { SetBit((int)UnionTag.Version, value); } }
        public bool IsInteger { get { return IsBitSet((int)UnionTag.Integer); } set { SetBit((int)UnionTag.Integer, value); } }
        public override string ToString()
        {
            if (IsUndefined) { return "{ Undefined }"; }

            StringBuilder value = new("{ ");

            if (UseTag) { value.Append(" Tag"); }
            if (IsBoolean) { value.Append(" Boolean"); }
            if (IsNumeric) { value.Append(" Numeric"); }
            if (IsDateTime) { value.Append(" DateTime"); }
            if (IsString) { value.Append(" String"); }
            if (IsBinary) { value.Append(" Binary"); }
            if (IsUuid) { value.Append(" Uuid"); }
            if (IsEntity) { value.Append(" Entity"); }
            if (IsVersion) { value.Append(" Version"); }
            if (IsInteger) { value.Append(" Integer"); }

            value.Append(" }");

            return value.ToString();
        }
        public List<UnionTag> ToColumnList()
        {
            List<UnionTag> tags = new();

            if (UseTag) { tags.Add(UnionTag.Tag); }
            if (IsBoolean) { tags.Add(UnionTag.Boolean); }
            if (IsNumeric) { tags.Add(UnionTag.Numeric); }
            if (IsDateTime) { tags.Add(UnionTag.DateTime); }
            if (IsString) { tags.Add(UnionTag.String); }
            if (IsBinary) { tags.Add(UnionTag.Binary); }
            if (IsEntity)
            {
                if (UseTypeCode)
                {
                    tags.Add(UnionTag.TypeCode);
                }
                tags.Add(UnionTag.Entity);
            }
            if (IsUuid) { tags.Add(UnionTag.Uuid); }
            if (IsVersion) { tags.Add(UnionTag.Version); }
            if (IsInteger) { tags.Add(UnionTag.Integer); }

            return tags;
        }
        public static Type MapToType(in UnionType union)
        {
            if (union.IsUnion) { return typeof(Union); }
            else if (union.IsBoolean) { return typeof(bool); }
            else if (union.IsNumeric) { return typeof(decimal); }
            else if (union.IsDateTime) { return typeof(DateTime); }
            else if (union.IsString) { return typeof(string); }
            else if (union.IsBinary) { return typeof(byte[]); }
            else if (union.IsUuid) { return typeof(Guid); }
            else if (union.IsEntity) { return typeof(Entity); }
            else if (union.IsVersion) { return typeof(ulong); }
            else if (union.IsInteger) { return typeof(int); }

            return null;
        }
        public static object GetDefaultValue(in Type type)
        {
            if (type == typeof(bool)) { return false; }
            else if (type == typeof(decimal)) { return 0.00M; }
            else if (type == typeof(DateTime)) { return new DateTime(1, 1, 1); }
            else if (type == typeof(string)) { return string.Empty; }
            else if (type == typeof(byte[])) { return Array.Empty<byte>(); }
            else if (type == typeof(Guid)) { return Guid.Empty; }
            else if (type == typeof(Entity)) { return Entity.Undefined; }
            else if (type == typeof(ulong)) { return 0UL; }
            else if (type == typeof(int)) { return 0; }
            else if (type == typeof(Union)) { return null; }

            return null;
        }
        public static object GetDefaultValue(UnionTag tag)
        {
            if (tag == UnionTag.Tag)
            {
                return new byte[1] { 0x01 }; // TYPE
            }
            else if (tag == UnionTag.Boolean)
            {
                return false; // L
            }
            else if (tag == UnionTag.Numeric)
            {
                return 0.00M; // N
            }
            else if (tag == UnionTag.DateTime)
            {
                return new DateTime(1, 1, 1); // T
            }
            else if (tag == UnionTag.String)
            {
                return string.Empty; // S
            }
            else if (tag == UnionTag.Binary)
            {
                return Array.Empty<byte>(); // B
            }
            else if (tag == UnionTag.Uuid)
            {
                return Guid.Empty.ToByteArray(); // U
            }
            else if (tag == UnionTag.Entity)
            {
                return Guid.Empty.ToByteArray(); // #
            }
            else if (tag == UnionTag.TypeCode)
            {
                return new byte[4] { 0x00, 0x00, 0x00, 0x00 };
            }
            else if (tag == UnionTag.Version)
            {
                return 0UL;
            }
            else if (tag == UnionTag.Integer)
            {
                return 0;
            }

            return null; // UnionTag.Undefined
        }
        public static string GetDefaultValueLiteral(UnionTag tag)
        {
            if (tag == UnionTag.Tag)
            {
                return "0x01"; // TYPE
            }
            else if (tag == UnionTag.Boolean)
            {
                return "0x00"; // L
            }
            else if (tag == UnionTag.Numeric)
            {
                return "0.00"; // N
            }
            else if (tag == UnionTag.DateTime)
            {
                return "0001-01-01T00:00:00"; // T
            }
            else if (tag == UnionTag.String)
            {
                return string.Empty; // S
            }
            else if (tag == UnionTag.Binary)
            {
                return "0x00"; // B
            }
            else if (tag == UnionTag.Uuid)
            {
                return $"0x{Convert.ToHexString(Guid.Empty.ToByteArray())}"; // U
            }
            else if (tag == UnionTag.Entity)
            {
                return $"0x{Convert.ToHexString(Guid.Empty.ToByteArray())}"; // #
            }
            else if (tag == UnionTag.TypeCode)
            {
                return "0x00000000";
            }
            else if (tag == UnionTag.Version)
            {
                return "0x0000000000000000";
            }
            else if (tag == UnionTag.Integer)
            {
                return "0x00000000";
            }

            return null; // UnionTag.Undefined
        }
        public static Type GetDataType(UnionTag tag)
        {
            if (tag == UnionTag.Tag)
            {
                return typeof(byte[]); // TYPE
            }
            else if (tag == UnionTag.Boolean)
            {
                return typeof(byte[]); // L
            }
            else if (tag == UnionTag.Numeric)
            {
                return typeof(decimal); // N
            }
            else if (tag == UnionTag.DateTime)
            {
                return typeof(DateTime); // T
            }
            else if (tag == UnionTag.String)
            {
                return typeof(string); // S
            }
            else if (tag == UnionTag.Binary)
            {
                return typeof(byte[]); // B
            }
            else if (tag == UnionTag.Uuid)
            {
                return typeof(byte[]); // U
            }
            else if (tag == UnionTag.Entity)
            {
                return typeof(byte[]); // #
            }
            else if (tag == UnionTag.TypeCode)
            {
                return typeof(byte[]);
            }
            else if (tag == UnionTag.Version)
            {
                return typeof(byte[]);
            }
            else if (tag == UnionTag.Integer)
            {
                return typeof(byte[]);
            }

            return null; // UnionTag.Undefined
        }
        public static string GetLiteral(UnionTag tag)
        {
            if (tag == UnionTag.Tag) { return "TYPE"; }
            else if (tag == UnionTag.Boolean) { return "L"; }
            else if (tag == UnionTag.Numeric) { return "N"; }
            else if (tag == UnionTag.DateTime) { return "T"; }
            else if (tag == UnionTag.String) { return "S"; }
            else if (tag == UnionTag.Binary) { return "B"; }
            else if (tag == UnionTag.Uuid) { return "U"; }
            else if (tag == UnionTag.Entity) { return "RRef"; }
            else if (tag == UnionTag.TypeCode) { return "TRef"; }
            else if (tag == UnionTag.Version) { return "V"; }
            else if (tag == UnionTag.Integer) { return "I"; }

            return string.Empty; // UnionTag.Undefined
        }
        public static string GetHexString(UnionTag tag)
        {
            return $"0x{Convert.ToHexString(new byte[] { (byte)tag })}";
        }
        public UnionTag GetSingleTagOrUndefined()
        {
            if (IsUnion) { return UnionTag.Tag; }
            else if (IsBoolean) { return UnionTag.Boolean; }
            else if (IsNumeric) { return UnionTag.Numeric; }
            else if (IsDateTime) { return UnionTag.DateTime; }
            else if (IsString) { return UnionTag.String; }
            else if (IsBinary) { return UnionTag.Binary; }
            else if (IsUuid) { return UnionTag.Uuid; }
            else if (IsEntity) { return UnionTag.Entity; }
            else if (IsVersion) { return UnionTag.Version; }
            else if (IsInteger) { return UnionTag.Integer; }

            return UnionTag.Undefined;
        }
        public static string GetDbTypeName(UnionTag tag)
        {
            if (tag == UnionTag.Tag) { return "binary(1)"; }
            else if (tag == UnionTag.Boolean) { return "binary(1)"; }
            else if (tag == UnionTag.Numeric) { return "numeric(16,4)"; }
            else if (tag == UnionTag.DateTime) { return "datetime2"; }
            else if (tag == UnionTag.String) { return "nvarchar(max)"; }
            else if (tag == UnionTag.Binary) { return "varbinary(max)"; }
            else if (tag == UnionTag.Uuid) { return "binary(16)"; }
            else if (tag == UnionTag.TypeCode) { return "binary(4)"; }
            else if (tag == UnionTag.Entity) { return "binary(16)"; }
            else if (tag == UnionTag.Version) { return "binary(8)"; }
            else if (tag == UnionTag.Integer) { return "binary(4)"; }

            return "varbinary(max)"; // UnionTag.Undefined
        }
    }
}