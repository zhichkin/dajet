using System.Reflection;

namespace DaJet.Model
{
    public sealed class IdentityMap
    {
        private readonly Dictionary<Guid, EntityObject> _map = new();
        public IdentityMap() { }
        public void Add(EntityObject entity)
        {
            IStateObject item = (IStateObject)entity;

            if (item is null) throw new ArgumentNullException(nameof(entity));

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
                IStateObject item = (IStateObject)sender;

                EntityObject entity = (EntityObject)sender;

                _map.Add(entity.Identity, entity);

                item.StateChanged -= NewItem_StateChanged;

                item.StateChanged += Item_StateChanged;
            }
        }
        private void Item_StateChanged(IPersistent sender, StateEventArgs args)
        {
            if (args.NewState == PersistentState.Deleted)
            {
                IStateObject item = (IStateObject)sender;

                _map.Remove(((EntityObject)item).Identity);

                item.StateChanged -= Item_StateChanged;
            }
        }
    }
}