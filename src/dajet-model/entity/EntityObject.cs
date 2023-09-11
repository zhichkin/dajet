using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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
    public delegate void StateChangedEventHandler(Persistent sender, StateEventArgs args);
    public delegate void StateChangingEventHandler(Persistent sender, StateEventArgs args);
    public abstract class Persistent : INotifyPropertyChanged
    {
        protected readonly IDataSource _source;
        protected PersistentState _state = PersistentState.New;
        protected Persistent(IDataSource source) { _source = source; }
        public void SetLoadingState() { _state = PersistentState.Loading; }
        public void SetOriginalState() { _state = PersistentState.Original; }
        public PersistentState State { get { return _state; } }
        public event StateChangedEventHandler StateChanged;
        public event StateChangingEventHandler StateChanging;
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnStateChanged(StateEventArgs args) { StateChanged?.Invoke(this, args); }
        private void OnStateChanging(StateEventArgs args) { StateChanging?.Invoke(this, args); }
        public void OnPropertyChanged(string propertyName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

        private void LazyLoad() { if (_state == PersistentState.Virtual) Load(); }
        protected TValue Get<TValue>(ref TValue storage) { LazyLoad(); return storage; }
        protected void Set<TValue>(TValue value, ref TValue storage, [CallerMemberName] string propertyName = null)
        {
            if (_state == PersistentState.Loading)
            {
                storage = value; return;
            }

            if (_state == PersistentState.New || _state == PersistentState.Changed)
            {
                storage = value;

                OnPropertyChanged(propertyName);

                return;
            }

            if (_state == PersistentState.Deleted) { return; }

            LazyLoad(); // this code is executed for Virtual state of reference objects

            // The code below is executed for Original state only

            if (_state != PersistentState.Original) { return; }

            bool changed;

            if (storage is not null)
            {
                changed = !storage.Equals(value);
            }
            else
            {
                changed = value is not null;
            }

            if (changed)
            {
                StateEventArgs args = new(PersistentState.Original, PersistentState.Changed);

                OnStateChanging(args);

                storage = value;

                _state = PersistentState.Changed;

                OnStateChanged(args);
            }

            OnPropertyChanged(propertyName);
        }

        #region "PERSISTENT INTERFACE IMPLEMENTATION"

        public void Save()
        {
            if (_state == PersistentState.New || _state == PersistentState.Changed)
            {
                StateEventArgs args = new(_state, PersistentState.Original);

                OnStateChanging(args);

                if (_state == PersistentState.New)
                {
                    _source?.Create(this);
                }
                else
                {
                    _source?.Update(this);
                }

                _state = PersistentState.Original;

                OnStateChanged(args);
            }
        }
        public void Kill()
        {
            if (_state == PersistentState.Original || _state == PersistentState.Changed || _state == PersistentState.Virtual)
            {
                StateEventArgs args = new(_state, PersistentState.Deleted);

                OnStateChanging(args);

                _source?.Delete(this);

                _state = PersistentState.Deleted;

                OnStateChanged(args);
            }
        }
        public void Load()
        {
            if (_state == PersistentState.Changed || _state == PersistentState.Original || _state == PersistentState.Virtual)
            {
                PersistentState old = _state;

                _state = PersistentState.Loading;

                StateEventArgs args = new(_state, PersistentState.Original);

                try
                {
                    OnStateChanging(args);

                    _source?.Select(this);

                    _state = PersistentState.Original;

                    OnStateChanged(args);
                }
                catch
                {
                    if (_state == PersistentState.Loading)
                    {
                        _state = old;
                    }

                    throw;
                }
            }
        }

        #endregion
    }
    public abstract class EntityObject : Persistent
    {
        private Guid _identity = Guid.NewGuid();
        protected EntityObject(IDataSource source) : base(source) { }
        [Key] public Guid Identity { get { return _identity; } }
        public void SetVirtualState(Guid identity)
        {
            _identity = identity;
            _state = PersistentState.Virtual;
        }
        public override string ToString() { return _identity.ToString(); }
        public override int GetHashCode() { return _identity.GetHashCode(); }
        public override bool Equals(object target)
        {
            if (target is not EntityObject test) { return false; }

            return GetType() == target.GetType() && _identity == test._identity;
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

        public async Task LoadAsync()
        {
            Persistent data = await _source.SelectAsync(GetType(), _identity);

            CopyFrom(in data);

            _state = PersistentState.Original;
        }
        protected abstract void CopyFrom(in Persistent data);
    }
}