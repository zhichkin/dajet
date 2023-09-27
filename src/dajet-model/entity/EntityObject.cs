using System;

namespace DaJet.Data
{
    public enum PersistentState : byte
    {
        New,      // объект только что создан в памяти, ещё не существует в источнике данных
        Virtual,  // объект существует в источнике данных, но ещё не загружены его свойства
        Original, // объект загружен из источника данных и ещё ни разу с тех пор не изменялся
        Changed,  // объект загружен из источника данных и с тех пор был уже изменен
        Deleted,  // объект удалён из источника данных, но пока ещё существует в памяти
        Loading   // объект в данный момент загружается из источника данных, это состояние
        // необходимо исключительно для случаев когда data mapper загружает из базы
        // данных объект, находящийсяв состоянии Virtual, чтобы иметь возможность
        // загружать значения свойств объекта обращаясь к ним напрямую и косвенно
        // вызывая метод Persistent.Set() - без этого состояния подобная стратегия
        // вызывает циклический вызов методов Persistent.Set(), Persistent.LazyLoad(),
        // IPersistent.Load(), IDataMapper.Select() и далее по кругу.
    }
    public abstract class EntityObject
    {
        private PersistentState _state = PersistentState.New;
        public int TypeCode { get; init; }
        public Guid Identity { get; init; }
        public bool IsEmpty() { return TypeCode > 0 && Identity == Guid.Empty; }
        public bool IsUndefined() { return TypeCode == 0 && Identity == Guid.Empty; }
        public bool IsNew() { return _state == PersistentState.New; }
        public bool IsChanged() { return _state == PersistentState.Changed; }
        public bool IsOriginal() { return _state == PersistentState.Original; }
        public void MarkAsOriginal() { _state = PersistentState.Original; }
        protected void Set<TValue>(TValue value, ref TValue storage)
        {
            if (_state == PersistentState.New || _state == PersistentState.Changed)
            {
                storage = value;
            }
            else if (_state == PersistentState.Original)
            {
                bool changed = (storage is null) ? value is not null : !storage.Equals(value);

                if (changed)
                {
                    storage = value;
                    
                    _state = PersistentState.Changed;
                }
            }
        }
        public override string ToString() { return $"{{{TypeCode}:{Identity}}}"; }
        public override int GetHashCode() { return Identity.GetHashCode(); }
        public override bool Equals(object target)
        {
            if (target is not EntityObject test) { return false; }

            return GetType() == target.GetType() && Identity == test.Identity;
        }
        public static bool operator ==(EntityObject left, EntityObject right)
        {
            if (ReferenceEquals(left, right)) { return true; }

            if (left is null || right is null) { return false; }

            return left.Equals(right);
        }
        public static bool operator !=(EntityObject left, EntityObject right)
        {
            return !(left == right);
        }
    }
}