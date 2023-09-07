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
    public class StateEventArgs : EventArgs
    {
        private readonly PersistentState _old;
        private readonly PersistentState _new;
        private StateEventArgs() { }
        public StateEventArgs(PersistentState old_state, PersistentState new_state)
        {
            _old = old_state;
            _new = new_state;
        }
        public PersistentState OldState { get { return _old; } }
        public PersistentState NewState { get { return _new; } }
    }
    public delegate void StateChangedEventHandler(EntityObject sender, StateEventArgs args);
    public delegate void StateChangingEventHandler(EntityObject sender, StateEventArgs args);

    public delegate void SavingEventHandler(EntityObject sender);
    public delegate void SavedEventHandler(EntityObject sender);
    public delegate void KillingEventHandler(EntityObject sender);
    public delegate void KilledEventHandler(EntityObject sender);
    public delegate void LoadedEventHandler(EntityObject sender);
}