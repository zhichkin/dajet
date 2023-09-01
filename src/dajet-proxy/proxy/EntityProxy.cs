using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DaJet.Model
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
    public interface IDataSource
    {
        void Create(EntityObject entity);
        void Update(EntityObject entity);
        void Delete(EntityObject entity);
        void Select(EntityObject entity);
    }
    public interface IStateObject
    {
        PersistentState State { get; }
        event StateChangedEventHandler StateChanged;
        event StateChangingEventHandler StateChanging;
    }
    public interface IPersistent
    {
        void Save();
        void Kill();
        void Load();
        event SavingEventHandler Saving;
        event SavedEventHandler Saved;
        event KillingEventHandler Killing;
        event KilledEventHandler Killed;
        event LoadedEventHandler Loaded;
    }
    public abstract class EntityProxy<TEntity> : EntityObject, IStateObject, IPersistent, INotifyPropertyChanged where TEntity : EntityObject
    {
        protected readonly TEntity _entity;
        protected readonly IDataSource _source;
        protected PersistentState _state = PersistentState.New;
        protected EntityProxy(TEntity entity) : base(entity.TypeCode, entity.Identity)
        {
            _entity = entity;
        }
        protected EntityProxy(TEntity entity, IDataSource source) : this(entity)
        {
            _source = source;
        }
        public Type GetEntityType() { return typeof(TEntity); }
        public EntityObject GetEntityObject() { return _entity; }
        public PersistentState State { get { return _state; } }
        public event StateChangedEventHandler StateChanged;
        public event StateChangingEventHandler StateChanging;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnStateChanged(StateEventArgs args) { StateChanged?.Invoke(this, args); }
        protected void OnStateChanging(StateEventArgs args) { StateChanging?.Invoke(this, args); }
        public void OnPropertyChanged(string propertyName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

        private void LazyLoad() { if (_state == PersistentState.Virtual) Load(); }
        protected TValue Get<TValue>(ref TValue storage) { LazyLoad(); return storage; }
        protected void Set<TValue>(TValue value, ref TValue storage, [CallerMemberName] string propertyName = null)
        {
            if (_state == PersistentState.Deleted) { return; }

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

        public void MarkAsVirtual()
        {
            if (_state == PersistentState.New)
            {
                _state = PersistentState.Virtual;
            }
            else
            {
                throw new InvalidOperationException($"State transition from [{_state}] to [{PersistentState.Virtual}] is not allowed!");
            }
        }
        public void MarkAsLoading()
        {
            if (_state == PersistentState.New || _state == PersistentState.Virtual)
            {
                _state = PersistentState.Loading;
            }
            else
            {
                throw new InvalidOperationException($"State transition from [{_state}] to [{PersistentState.Loading}] is not allowed!");
            }
        }
        public void MarkAsOriginal()
        {
            if (_state == PersistentState.Loading)
            {
                _state = PersistentState.Original;
            }
            else
            {
                throw new InvalidOperationException($"State transition from [{_state}] to [{PersistentState.Original}] is not allowed!");
            }
        }

        #region "PERSISTENT INTERFACE IMPLEMENTATION"

        public event SavingEventHandler Saving;
        public event SavedEventHandler Saved;
        public event KillingEventHandler Killing;
        public event KilledEventHandler Killed;
        public event LoadedEventHandler Loaded;
        private void OnSaving() { Saving?.Invoke(this); }
        private void OnSaved() { Saved?.Invoke(this); }
        private void OnKilling()
        {
            if (Killing == null) return;

            Delegate[] list = Killing.GetInvocationList();

            int count = list.Length;

            while (count > 0)
            {
                if (list[--count] is KillingEventHandler handler)
                {
                    handler(this);
                }
            }
        }
        private void OnKilled() { Killed?.Invoke(this); }
        private void OnLoaded() { Loaded?.Invoke(this); }

        public virtual void Save()
        {
            if (_state == PersistentState.New || _state == PersistentState.Changed)
            {
                OnSaving();

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

            OnSaved(); // is invoked for all states to notify dependent classes on event
        }
        public virtual void Kill()
        {
            if (_state == PersistentState.Original || _state == PersistentState.Changed || _state == PersistentState.Virtual)
            {
                OnKilling();

                StateEventArgs args = new(_state, PersistentState.Deleted);

                OnStateChanging(args);

                _source?.Delete(this);

                _state = PersistentState.Deleted;

                OnStateChanged(args);

                OnKilled();
            }
        }
        public virtual void Load()
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

                    OnLoaded();
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
}