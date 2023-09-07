using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace DaJet.Data
{
    public abstract class EntityObject
    {
        private PersistentState _state;
        private readonly Guid _identity;
        protected readonly IDataSource _source;
        protected EntityObject(IDataSource source)
        {
            _source = source;
            _identity = Guid.NewGuid();
            _state = PersistentState.New;
        }
        protected EntityObject(IDataSource source, Guid identity)
        {
            _source = source;
            _identity = identity;
            _state = PersistentState.Virtual;
        }
        [Key] public Guid Identity { get { return _identity; } }
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

        public PersistentState GetState() { return _state; }
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

        public void Save()
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
        public void Kill()
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