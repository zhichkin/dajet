namespace DaJet.Model
{
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

    public delegate void StateChangingEventHandler(IPersistent sender, StateEventArgs args);
    public delegate void StateChangedEventHandler(IPersistent sender, StateEventArgs args);

    public delegate void SavingEventHandler(IPersistent sender);
    public delegate void SavedEventHandler(IPersistent sender);
    public delegate void KillingEventHandler(IPersistent sender);
    public delegate void KilledEventHandler(IPersistent sender);
    public delegate void LoadedEventHandler(IPersistent sender);
}