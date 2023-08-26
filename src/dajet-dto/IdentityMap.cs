namespace DaJet.Model
{
    public sealed class IdentityMap
    {
        private readonly Dictionary<Guid, EntityObject> _map = new();
        public IdentityMap() { }
        public void Add(EntityObject item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            if (item.State == PersistentState.New)
            {
                item.StateChanged += NewItem_StateChanged;
            }
            else if (item.State == PersistentState.Virtual)
            {
                item.StateChanged += Item_StateChanged;
            }
        }
        public bool TryGet(Guid identity, ref EntityObject item)
        {
            return _map.TryGetValue(identity, out item);
        }
        private void NewItem_StateChanged(IPersistent sender, StateEventArgs args)
        {
            if (args.OldState == PersistentState.New && args.NewState == PersistentState.Original)
            {
                EntityObject item = (EntityObject)sender;

                _map.Add(item.Identity, item);

                item.StateChanged -= NewItem_StateChanged;

                item.StateChanged += Item_StateChanged;
            }
        }
        private void Item_StateChanged(IPersistent sender, StateEventArgs args)
        {
            if (args.NewState == PersistentState.Deleted)
            {
                EntityObject item = (EntityObject)sender;

                _map.Remove(item.Identity);

                item.StateChanged -= Item_StateChanged;
            }
        }
    }
}