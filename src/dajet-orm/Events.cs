using System;

namespace DaJet.Orm
{
    public class StateEventArgs : EventArgs
    {
        private PersistentState old_state;
        private PersistentState new_state;

        private StateEventArgs() { }

        public StateEventArgs(PersistentState old_state, PersistentState new_state)
        {
            this.old_state = old_state;
            this.new_state = new_state;
        }

        public PersistentState OldState { get { return old_state; } }
        public PersistentState NewState { get { return new_state; } }
    }
    public delegate void StateChangingEventHandler(IPersistent sender, StateEventArgs args);
    public delegate void StateChangedEventHandler(IPersistent sender, StateEventArgs args);

    public delegate void SavingEventHandler(IPersistent entity);
    public delegate void SavedEventHandler(IPersistent entity);
    public delegate void KillingEventHandler(IPersistent entity);
    public delegate void KilledEventHandler(IPersistent entity);
    public delegate void LoadedEventHandler(IPersistent entity);
}