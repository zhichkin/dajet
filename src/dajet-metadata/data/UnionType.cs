using System;
using System.Collections.Generic;
using System.Text;

namespace DaJet.Data
{
    public sealed class UnionType
    {
        private uint _flags = uint.MinValue;
        private int _typeCode = -1;
        public UnionType() { }
        private void SetBit(int position, bool value)
        {
            if (value) { _flags |= (1U << position); } else { _flags &= ~(1U << position); }
        }
        private bool IsBitSet(int position)
        {
            return (_flags & (1U << position)) == (1U << position);
        }
        public void Merge(in UnionType union)
        {
            _flags |= union._flags;
            TypeCode = union.TypeCode;
        }
        public void Clear()
        {
            _flags = uint.MinValue;
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
        public bool IsUndefined { get { return (_flags == uint.MinValue); } }
        public bool UseTag { get { return IsBitSet((int)UnionTag.Tag); } set { SetBit((int)UnionTag.Tag, value); } }
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
        public static Type MapToType(in UnionType union)
        {
            if (union.IsUnion)
            {
                return typeof(Union);
            }
            else if (union.IsBoolean)
            {
                return typeof(bool);
            }
            else if (union.IsNumeric)
            {
                return typeof(decimal);
            }
            else if (union.IsDateTime)
            {
                return typeof(DateTime);
            }
            else if (union.IsString)
            {
                return typeof(string);
            }
            else if (union.IsBinary)
            {
                return typeof(byte[]);
            }
            else if (union.IsUuid)
            {
                return typeof(Guid);
            }
            else if (union.IsEntity)
            {
                return typeof(Entity);
            }
            else if (union.IsVersion)
            {
                return typeof(ulong);
            }
            else if (union.IsInteger)
            {
                return typeof(int);
            }

            return null;
        }
        public static object GetDefaultValue(in Type type)
        {
            if (type == typeof(bool))
            {
                return false;
            }
            else if (type == typeof(decimal))
            {
                return 0.00M;
            }
            else if (type == typeof(DateTime))
            {
                return new DateTime(1, 1, 1);
            }
            else if (type == typeof(string))
            {
                return string.Empty;
            }
            else if (type == typeof(byte[]))
            {
                return Array.Empty<byte>();
            }
            else if (type == typeof(Guid))
            {
                return Guid.Empty;
            }
            else if (type == typeof(Entity))
            {
                return Entity.Undefined;
            }
            else if (type == typeof(ulong))
            {
                return 0UL;
            }
            else if (type == typeof(int))
            {
                return 0;
            }

            return null;
        }
        public List<UnionTag> ToList()
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
                if (TypeCode == 0)
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
    }
}