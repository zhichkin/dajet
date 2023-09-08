using DaJet.Data;
using System;
using System.Collections.Generic;

namespace DaJet.Model
{
    public sealed class IdentityMap
    {
        private readonly Dictionary<Guid, EntityObject> _map = new();
        public IdentityMap() { }
        public void Add(EntityObject entity)
        {
            if (entity is null) throw new ArgumentNullException(nameof(entity));

            if (entity.State == PersistentState.New)
            {
                entity.StateChanged += NewItem_StateChanged;
            }
            else if (entity.State == PersistentState.Virtual)
            {
                entity.StateChanged += Item_StateChanged;
            }
        }
        public bool TryGet(Guid identity, ref EntityObject item)
        {
            return _map.TryGetValue(identity, out item);
        }
        private void NewItem_StateChanged(Persistent sender, StateEventArgs args)
        {
            if (sender is not EntityObject entity)
            {
                throw new InvalidOperationException();
            }

            if (args.OldState == PersistentState.New && args.NewState == PersistentState.Original)
            {
                _map.Add(entity.Identity, entity);

                entity.StateChanged -= NewItem_StateChanged;

                entity.StateChanged += Item_StateChanged;
            }
        }
        private void Item_StateChanged(Persistent sender, StateEventArgs args)
        {
            if (sender is not EntityObject entity)
            {
                throw new InvalidOperationException();
            }

            if (args.NewState == PersistentState.Deleted)
            {
                _map.Remove(entity.Identity);

                entity.StateChanged -= Item_StateChanged;
            }
        }
        public void Update(Guid identity, EntityObject entity)
        {
            _map[identity] = entity;
        }
    }
}