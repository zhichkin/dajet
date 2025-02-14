using System.Collections.Generic;
using System.Dynamic;

namespace DaJet.Data
{
    /// <summary>
    /// Универсальный объект представления данных, имеющий произвольное количество свойств, создаваемых или удаляемых программно.
    /// Экземпляры данного класса предназначены для представления данных запросов, объектов конфигураций баз данных, а также
    /// любых иных динамически изменяемых структур данных и переноса этих данных между программными компонентами.
    /// <br/>Класс <see cref="DataObject"/> наследует от класса <see cref="DynamicObject"/>, что даёт возможность использовать
    /// позднее связывание (late binding) при обращении к свойствам его экземпляров под управлением DLR (Dynamic Language Runtime).
    /// При этом следует иметь ввиду, что поведение оператора присваивания значения несуществующему свойству объекта в среде DLR
    /// может вызвать исключение. Смотри примечания к методам <see cref="DataObject.SetValue(string, object)"/>
    /// и <see cref="DataObject.TrySetValue(string, object)"/>.
    /// </summary>
    public sealed class DataObject : DynamicObject
    {
        private int _code = 0;
        private string _name = string.Empty;
        private readonly List<string> _names;
        private readonly List<object> _values;
        private readonly Dictionary<string, int> _map;
        //TODO: add a list of property data type tags - useful for JSON serialization
        public DataObject()
        {
            _names = new(); _values = new(); _map = new();
        }
        public DataObject(int capacity)
        {
            _names = new(capacity); _values = new(capacity); _map = new(capacity);
        }
        public int GetCode() { return _code; }
        /// <summary>
        /// <b>Служебное свойство</b>
        /// <br/>Числовой код типа данных, который представляет данный объект.
        /// </summary>
        public void SetCode(int code) { _code = code; }
        public string GetName() { return _name; }
        /// <summary>
        /// <b>Служебное свойство</b>
        /// <br/>Имя типа данных или табличной части, например, "Справочник.Номенклатура".
        /// </summary>
        public void SetName(string name) { _name = name is null ? string.Empty : name; }
        public void SetCodeAndName(int code, string name)
        {
            _code = code; _name = name is null ? string.Empty : name;
        }
        public void ClearCodeAndName() { _code = 0; _name = string.Empty; }
        public int Count() { return _values.Count; }
        public bool Contains(string name) { return _map.ContainsKey(name); }
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
        /// <br/>An exception may be thrown by DLR if the property does not exist.
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

        public DataObject Copy()
        {
            int count = this.Count();

            DataObject copy = new(this.Count());

            string name;
            object value;

            for (int i = 0; i < count; i++)
            {
                name = this.GetName(i);
                value = this.GetValue(i);

                if (value is DataObject record)
                {
                    copy.SetNameAndValue(in name, record.Copy());
                }
                else if (value is List<DataObject> table)
                {
                    copy.SetNameAndValue(in name, CopyArray(in table));
                }
                else
                {
                    copy.SetNameAndValue(in name, in value);
                }
            }

            return copy;
        }
        private List<DataObject> CopyArray(in List<DataObject> source)
        {
            List<DataObject> target = new(source.Count);

            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i].Copy());
            }

            return target;
        }
        private void SetNameAndValue(in string name, in object value)
        {
            _names.Add(name);
            _values.Add(value);
            _map.Add(name, _values.Count - 1);
        }
    }
}