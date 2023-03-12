using System;
using System.Collections.Generic;

namespace DaJet.Orm
{
    public sealed class IdentityMap : IIdentityMap
    {
        public IdentityMap() { }

        private Dictionary<Guid, IReferenceObject> map = new Dictionary<Guid, IReferenceObject>();

        public void Add(IReferenceObject item)
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

        public bool Get(Guid identity, ref IReferenceObject item)
        {
            return map.TryGetValue(identity, out item);
        }

        private void NewItem_StateChanged(IPersistent sender, StateEventArgs args)
        {
            if (args.OldState == PersistentState.New && args.NewState == PersistentState.Original)
            {
                IReferenceObject item = (IReferenceObject)sender;

                map.Add(item.Identity, item);

                item.StateChanged -= NewItem_StateChanged;

                item.StateChanged += Item_StateChanged;
            }
        }

        private void Item_StateChanged(IPersistent sender, StateEventArgs args)
        {
            if (args.NewState == PersistentState.Deleted)
            {
                IReferenceObject item = (IReferenceObject)sender;

                map.Remove(item.Identity);

                item.StateChanged -= Item_StateChanged;
            }
        }
    }
}