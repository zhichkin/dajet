using System;
using System.Collections.Generic;

namespace DaJet.Data
{
    public sealed class DataObject
    {
        private readonly List<string> _names;
        private readonly List<object> _values;
        private readonly Dictionary<string, int> _map;
        public DataObject()
        {
            _names = new(); _values = new(); _map = new();
        }
        public DataObject(int capacity)
        {
            _names = new(capacity); _values = new(capacity); _map = new(capacity);
        }
        public int Count() { return _values.Count; }
        public void Clear() { _names.Clear(); _values.Clear(); _map.Clear(); }
        public string GetName(int i) { return _names[i]; }
        public object GetValue(int i) { return _values[i]; }
        public object GetValue(string name) { return _values[_map[name]]; }
        public void SetValue(string name, object value)
        {
            if (_map.TryGetValue(name, out int ordinal))
            {
                _values[ordinal] = value;
            }
            else
            {
                _names.Add(name);
                _values.Add(value);
                _map.Add(name, _values.Count - 1);
            }
        }

        #region "TYPED VALUE GETTERS"
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
    }
}