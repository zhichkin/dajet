using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DaJet.Orm
{
    public abstract class Persistent : IPersistent, INotifyPropertyChanged
    {
        protected IDataMapper mapper;
        protected PersistentState state = PersistentState.New;

        private Persistent() { }

        protected Persistent(IDataMapper mapper)
        {
            this.mapper = mapper;
        }

        public PersistentState State
        {
            get { return state; }
            set
            {
                if (state == PersistentState.New && value == PersistentState.Loading)
                {
                    state = value;
                }
                else if (state == PersistentState.Loading && value == PersistentState.Original)
                {
                    state = value;
                    UpdateKeyValues();
                }
                else
                {
                    throw new NotSupportedException("The transition from the current state to the new one is not allowed!"
                        + Environment.NewLine
                        + string.Format("Current state: {0}. New state: {1}.", state.ToString(), value.ToString()));
                }
            }
        }
        
        protected virtual void UpdateKeyValues()
        {
            // Compound keys can have fields changeable by user code.
            // When changed key is stored to the database, object's key values in memory must be synchronized.
        }

        protected void Set<TValue>(TValue value, ref TValue storage, [CallerMemberName] string propertyName = null)
        {
            if (state == PersistentState.Deleted) return;

            if (state == PersistentState.Loading)
            {
                storage = value; return;
            }

            if (state == PersistentState.New || state == PersistentState.Changed)
            {
                storage = value;
                OnPropertyChanged(propertyName);
                return;
            }

            LazyLoad(); // this code is executed for Virtual state of reference objects

            // The code below is executed for Original state only

            if (state != PersistentState.Original) return;

            bool changed = false;

            if (storage != null)
            {
                changed = !storage.Equals(value);
            }
            else
            {
                changed = (value != null);
            }

            if (changed)
            {
                StateEventArgs args = new StateEventArgs(PersistentState.Original, PersistentState.Changed);

                OnStateChanging(args);

                storage = value;

                state = PersistentState.Changed;

                OnStateChanged(args);
            }
            OnPropertyChanged(propertyName);
        }

        protected TValue Get<TValue>(ref TValue storage)
        {
            LazyLoad(); return storage;
        }

        public void OnPropertyChanged(string propertyName)
        {
            if (propertyName == null) throw new ArgumentNullException("propertyName");
            if(string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentOutOfRangeException("propertyName");
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        # region " state events handling "

        public event StateChangedEventHandler StateChanged;
        public event StateChangingEventHandler StateChanging;
        protected void OnStateChanging(StateEventArgs args)
        {
            if (StateChanging != null) StateChanging(this, args);
        }
        protected void OnStateChanged(StateEventArgs args)
        {
            if (args.NewState == PersistentState.Original)
            {
                UpdateKeyValues();
            }
            if (StateChanged != null) StateChanged(this, args);
        }
        
        # endregion

        private void LazyLoad() { if (state == PersistentState.Virtual) Load(); }

        # region " ActiveRecord "

        public event SavingEventHandler Saving;
        public event SavedEventHandler Saved;
        public event KillingEventHandler Killing;
        public event KilledEventHandler Killed;
        public event LoadedEventHandler Loaded;
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnSaving()
        {
            if (Saving != null) Saving(this);
        }
        private void OnSaved()
        {
            if (Saved != null) Saved(this);
        }
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
        private void OnKilled()
        {
            if (Killed != null) Killed(this);
        }
        private void OnLoaded()
        {
            if (Loaded != null) Loaded(this);
        }
        
        public virtual void Save()
        {
            if (state == PersistentState.New || state == PersistentState.Changed)
            {
                OnSaving();

                StateEventArgs args = new StateEventArgs(state, PersistentState.Original);

                OnStateChanging(args);

                if (state == PersistentState.New)
                {
                    mapper.Insert(this);
                }
                else
                {
                    mapper.Update(this);
                }

                state = PersistentState.Original;

                OnStateChanged(args);
            }
            OnSaved(); // is invoked for all states to notify dependent classes on event
        }
        public virtual void Kill()
        {
            if (state == PersistentState.Original || state == PersistentState.Changed || state == PersistentState.Virtual)
            {
                OnKilling();

                StateEventArgs args = new StateEventArgs(state, PersistentState.Deleted);

                OnStateChanging(args);

                mapper.Delete(this);

                state = PersistentState.Deleted;

                OnStateChanged(args);

                OnKilled();
            }
        }
        public virtual void Load()
        {
            if (state == PersistentState.Changed || state == PersistentState.Original || state == PersistentState.Virtual)
            {
                PersistentState old = state;

                state = PersistentState.Loading;

                StateEventArgs args = new StateEventArgs(state, PersistentState.Original);

                try
                {
                    OnStateChanging(args);

                    mapper.Select(this);

                    state = PersistentState.Original;

                    OnStateChanged(args);

                    OnLoaded();
                }
                catch
                {
                    if (state == PersistentState.Loading) state = old; throw;
                }
            }
        }

        # endregion
    }
}