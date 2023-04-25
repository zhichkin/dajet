using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace DaJet.Data
{
    public sealed class DataRecord : IDataRecord
    {
        private readonly List<object> _values = new();
        private readonly Dictionary<string, int> _names = new();
        public DataRecord() { }
        public object this[int i]
        {
            get { return _values[i]; }
            set { _values[i] = value; }
        }
        public object this[string name]
        {
            get { return _values[_names[name]]; }
            set { _values[_names[name]] = value; }
        }
        public int FieldCount { get { return _values.Count; } }
        public void Clear() { _names.Clear(); _values.Clear(); }
        public void SetValue(string name, object value)
        {
            if (_names.TryGetValue(name, out int ordinal))
            {
                _values[ordinal] = value;
            }
            else
            {
                _values.Add(value);
                _names.Add(name, _values.Count - 1);
            }
        }
        public string GetName(int i)
        {
            foreach (var item in _names)
            {
                if (item.Value == i)
                {
                    return item.Key;
                }
            }
            throw new IndexOutOfRangeException(nameof(i));
        }
        public object GetValue(int i) { return _values[i]; }
        public int GetOrdinal(string name) { return _names[name]; }

        #region "COLUMN METADATA"
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
        public Type GetFieldType(int i) { return _values[i].GetType(); }
        public string GetDataTypeName(int i) { return _values[i].GetType().Name; }
        #endregion

        #region "VALUE GETTERS"
        public bool IsDBNull(int i) { return DBNull.Value.Equals(_values[i]); }
        public byte GetByte(int i) { return (byte)_values[i]; }
        public bool GetBoolean(int i) { return (bool)_values[i]; }
        public string GetString(int i) { return (string)_values[i]; }

        public short GetInt16(int i) { return (short)_values[i]; }
        public int GetInt32(int i) { return (int)_values[i]; }
        public long GetInt64(int i) { return (long)_values[i]; }

        public float GetFloat(int i) { return (float)_values[i]; }
        public double GetDouble(int i) { return (double)_values[i]; }
        public decimal GetDecimal(int i) { return (decimal)_values[i]; }

        public Guid GetGuid(int i) { return (Guid)_values[i]; }
        public DateTime GetDateTime(int i) { return (DateTime)_values[i]; }
        #endregion

        #region "NOT IMPLEMENTED (IDataRecord interface)"
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