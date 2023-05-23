using System;
using System.Collections.Generic;

namespace DaJet.Model
{
    public sealed class IdentityMap
    {
        private readonly Dictionary<Guid, ReferenceObject> _map = new();
        public IdentityMap() { }
        public void Add(ReferenceObject item)
        {
            if (item == null) throw new ArgumentNullException("item");

            if (item.State == PersistentState.New)
            {
                item.StateChanged += NewItem_StateChanged;
            }
            else if (item.State == PersistentState.Virtual)
            {
                item.StateChanged += Item_StateChanged;
            }
        }
        public bool Get(Guid identity, ref ReferenceObject item)
        {
            return _map.TryGetValue(identity, out item);
        }
        private void NewItem_StateChanged(IPersistent sender, StateEventArgs args)
        {
            if (args.OldState == PersistentState.New && args.NewState == PersistentState.Original)
            {
                ReferenceObject item = (ReferenceObject)sender;

                _map.Add(item.Ref, item);

                item.StateChanged -= NewItem_StateChanged;

                item.StateChanged += Item_StateChanged;
            }
        }
        private void Item_StateChanged(IPersistent sender, StateEventArgs args)
        {
            if (args.NewState == PersistentState.Deleted)
            {
                ReferenceObject item = (ReferenceObject)sender;

                _map.Remove(item.Ref);

                item.StateChanged -= Item_StateChanged;
            }
        }
    }
}