using System.Collections.Generic;
using System.Dynamic;

namespace DaJet.Data
{
    public sealed class DataObject : DynamicObject
    {
        private int _code = 0;
        private string _name = string.Empty;
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
        public int GetCode() { return _code; }
        public void SetCode(int code) { _code = code; }
        public string GetName() { return _name; }
        public void SetName(string name) { _name = name is null ? string.Empty : name; }
        public void SetCodeAndName(int code, string name)
        {
            _code = code; _name = name is null ? string.Empty : name;
        }
        public void ClearCodeAndName() { _code = 0; _name = string.Empty; }
        public int Count() { return _values.Count; }
        public void Clear() { _names.Clear(); _values.Clear(); _map.Clear(); }
        public void Remove(string name)
        {
            if (_map.TryGetValue(name, out int ordinal))
            {
                _ = _map.Remove(name);
                _names.RemoveAt(ordinal);
                _values.RemoveAt(ordinal);

                int count = _names.Count;

                if (count == 0) { return; }

                for (int i = 0; i < count; i++)
                {
                    if (i >= ordinal)
                    {
                        _map[_names[i]] = i;
                    }
                }
            }
        }
        public string GetName(int i) { return _names[i]; }
        public object GetValue(int i) { return _values[i]; }
        public object GetValue(string name) { return _values[_map[name]]; }
        public bool TryGetValue(string name, out object value)
        {
            if (_map.TryGetValue(name, out int ordinal))
            {
                value = _values[ordinal]; return true;
            }

            value = null;
            return false;
        }
        
        /// <summary>
        /// Replaces an existing property value or adds a new
        /// property to the object using the provided value.
        /// </summary>
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
        
        /// <summary>
        /// Intended for use by TrySetMember, respecting DLR semantics.
        /// <br/>An exception may be thrown if the property does not exist.
        /// </summary>
        public bool TrySetValue(string name, object value)
        {
            if (_map.TryGetValue(name, out int ordinal))
            {
                _values[ordinal] = value; return true;
            }

            return false;
        }

        #region "DYNAMIC OBJECT IMPLEMENTATION"
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return _names;
        }
        public override bool TryGetMember(GetMemberBinder binder, out object value)
        {
            return TryGetValue(binder.Name, out value);
        }
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            return TrySetValue(binder.Name, value);
        }
        #endregion
    }
}