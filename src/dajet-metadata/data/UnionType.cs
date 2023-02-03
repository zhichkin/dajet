namespace DaJet.Data
{
    public struct UnionType
    {
        private uint _flags = uint.MinValue;
        public UnionType() { }
        private void SetBit(int position, bool value)
        {
            if (value) { _flags |= 1U << position; } else { _flags &= ~(1U << position); }
        }
        private bool IsBitSet(int position)
        {
            return (_flags & (1U << position)) == 1U << position;
        }
        public void Merge(UnionType union)
        {
            _flags |= union._flags;
            TypeCode = union.TypeCode;
        }
        private int _typeCode = -1;
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
                        // ignore
                    }
                }
            }
        }
        public bool IsUnion
        {
            get
            {
                int count = 0;
                if (IsUuid) { count++; }     // _B
                if (IsVersion) { count++; }  // _B
                if (IsBoolean) { count++; }  // _L
                if (IsNumeric) { count++; }  // _N
                if (IsDateTime) { count++; } // _T
                if (IsString) { count++; }   // _S
                if (IsBinary) { count++; }   // _B
                if (IsEntity) { count++; }   // _TRef + _RRef
                return (count > 1 || IsEntity && TypeCode == 0); // _TYPE
            }
        }
        public bool HasTag { get { return IsBitSet((int)UnionTag.Tag); } set { SetBit((int)UnionTag.Tag, value); } }
        public bool IsBoolean { get { return IsBitSet((int)UnionTag.Boolean); } set { SetBit((int)UnionTag.Boolean, value); } }
        public bool IsNumeric { get { return IsBitSet((int)UnionTag.Numeric); } set { SetBit((int)UnionTag.Numeric, value); } }
        public bool IsDateTime { get { return IsBitSet((int)UnionTag.DateTime); } set { SetBit((int)UnionTag.DateTime, value); } }
        public bool IsString { get { return IsBitSet((int)UnionTag.String); } set { SetBit((int)UnionTag.String, value); } }
        public bool IsBinary { get { return IsBitSet((int)UnionTag.Binary); } set { SetBit((int)UnionTag.Binary, value); } }
        public bool IsUuid { get { return IsBitSet((int)UnionTag.Uuid); } set { SetBit((int)UnionTag.Uuid, value); } }
        public bool IsEntity { get { return IsBitSet((int)UnionTag.Entity); } set { SetBit((int)UnionTag.Entity, value); } }
        public bool IsVersion { get { return IsBitSet((int)UnionTag.Version); } set { SetBit((int)UnionTag.Version, value); } }
    }
}