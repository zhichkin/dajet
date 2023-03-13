using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DaJet.Model
{
    public interface IPersistent
    {
        PersistentState State { get; }
        event StateChangingEventHandler StateChanging;
        event StateChangedEventHandler StateChanged;
        void Save();
        void Kill();
        void Load();
        event SavingEventHandler Saving;
        event SavedEventHandler Saved;
        event KillingEventHandler Killing;
        event KilledEventHandler Killed;
        event LoadedEventHandler Loaded;
    }
    public abstract class PersistentObject : IPersistent, INotifyPropertyChanged
    {
        protected IDataMapper _mapper;
        protected PersistentState _state = PersistentState.New;
        private PersistentObject() { }
        protected PersistentObject(IDataMapper mapper) { _mapper = mapper; }
        public PersistentState State { get { return _state; } }
        //public void SetStateLoading()
        //{
        //    if (_state == PersistentState.New)
        //    {
        //        _state = PersistentState.Loading;
        //    }
        //    else
        //    {
        //        throw new InvalidOperationException($"State transition from [{_state}] to [{PersistentState.Loading}] is not allowed!");
        //    }
        //}
        //set
        //{
        //    if (state == PersistentState.New && value == PersistentState.Loading)
        //    {
        //        state = value;
        //    }
        //    else if (state == PersistentState.Loading && value == PersistentState.Original)
        //    {
        //        state = value;
        //        UpdateKeyValues();
        //    }
        //    else
        //    {
        //        throw new NotSupportedException("The transition from the current state to the new one is not allowed!"
        //            + Environment.NewLine
        //            + string.Format("Current state: {0}. New state: {1}.", state.ToString(), value.ToString()));
        //    }
        //}
        protected virtual void UpdateKeyValues()
        {
            // Compound keys can have fields changeable by user code.
            // When changed key is stored to the database, object's key values in memory must be synchronized.
        }
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
        public void OnPropertyChanged(string propertyName)
        {
            if (propertyName is null) throw new ArgumentNullException(nameof(propertyName));
            if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentOutOfRangeException(nameof(propertyName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region "PERSISTENT STATE EVENTS HANDLING"

        public event StateChangedEventHandler StateChanged;
        public event StateChangingEventHandler StateChanging;
        protected void OnStateChanging(StateEventArgs args)
        {
            StateChanging?.Invoke(this, args);
        }
        protected void OnStateChanged(StateEventArgs args)
        {
            if (args.NewState == PersistentState.Original)
            {
                UpdateKeyValues();
            }
            StateChanged?.Invoke(this, args);
        }

        #endregion

        private void LazyLoad() { if (_state == PersistentState.Virtual) Load(); }

        #region "ACTIVE RECORD METHODS AND EVENTS"

        public event SavingEventHandler Saving;
        public event SavedEventHandler Saved;
        public event KillingEventHandler Killing;
        public event KilledEventHandler Killed;
        public event LoadedEventHandler Loaded;
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnSaving() { Saving?.Invoke(this); }
        private void OnSaved() { Saved?.Invoke(this); }
        private void OnKilling()
        {
            if (Killing == null) return;

            Delegate[] list = Killing.GetInvocationList();

            int count = list.Length;

            while (count > 0)
            {
                count--;
                ((KillingEventHandler)list[count])(this);
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
                    _mapper.Insert(this);
                }
                else
                {
                    _mapper.Update(this);
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

                _mapper.Delete(this);

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

                    _mapper.Select(this);

                    _state = PersistentState.Original;

                    OnStateChanged(args);

                    OnLoaded();
                }
                catch
                {
                    if (_state == PersistentState.Loading) { _state = old; }
                    throw;
                }
            }
        }

        #endregion
    }
}