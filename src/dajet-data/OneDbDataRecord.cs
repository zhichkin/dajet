using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace DaJet.Data
{
    public sealed class OneDbDataRecord : IDataRecord
    {
        private readonly EntityMapper _map;
        private readonly DbDataReader _reader;
        public OneDbDataRecord(in DbDataReader reader, in EntityMapper map) : base()
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }
        public object this[int ordinal] { get { return GetValue(ordinal); } }
        public object this[string name]
        {
            get
            {
                PropertyMapper property;

                for (int ordinal = 0; ordinal < _map.Properties.Count; ordinal++)
                {
                    property = _map.Properties[ordinal];

                    if (property.Name == name)
                    {
                        return property.GetValue(_reader)!;
                    }
                }

                throw new IndexOutOfRangeException(name);
            }
        }
        public int FieldCount { get { return _map.Properties.Count; } }
        public string GetName(int ordinal) { return _map.Properties[ordinal].Name; }
        public object GetValue(int ordinal) { return _map.Properties[ordinal].GetValue(_reader); }
        public int GetOrdinal(string name)
        {
            PropertyMapper property;

            for (int ordinal = 0; ordinal < _map.Properties.Count; ordinal++)
            {
                property = _map.Properties[ordinal];

                if (property.Name == name)
                {
                    return ordinal;
                }
            }

            throw new IndexOutOfRangeException(name);
        }

        #region "COLUMN METADATA"
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        public Type GetFieldType(int ordinal) { return _map.Properties[ordinal].Type; }
        public string GetDataTypeName(int ordinal) { return _map.Properties[ordinal].Type.Name; }
        #endregion

        #region "VALUE GETTERS"
        public bool IsDBNull(int ordinal) { return GetValue(ordinal) is null; }
        public byte GetByte(int ordinal) { throw new NotImplementedException(); }
        public bool GetBoolean(int ordinal) { return (bool)GetValue(ordinal); }
        public string GetString(int ordinal) { return (string)GetValue(ordinal); }

        public float GetFloat(int ordinal) { return (float)GetValue(ordinal); }
        public double GetDouble(int ordinal) { return (double)GetValue(ordinal); }
        public decimal GetDecimal(int ordinal) { return (decimal)GetValue(ordinal); }

        public short GetInt16(int ordinal) { return (short)GetValue(ordinal); }
        public int GetInt32(int ordinal) { return (int)GetValue(ordinal); }
        public long GetInt64(int ordinal) { return (long)GetValue(ordinal); }

        public Guid GetGuid(int ordinal) { return (Guid)GetValue(ordinal); }
        public DateTime GetDateTime(int ordinal) { return (DateTime)GetValue(ordinal); }
        #endregion

        #region "NOT IMPLEMENTED"
        public IDataReader GetData(int i)
        {
            throw new NotImplementedException();
        }
        public int GetValues(object[] values)
        {
            throw new NotImplementedException();
        }
        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }
        public char GetChar(int i)
        {
            throw new NotImplementedException();
        }
        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}