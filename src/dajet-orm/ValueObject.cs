using System;

namespace DaJet.Orm
{
    public abstract class ValueObject : Persistent, IValueObject
    {
        private void CheckState(PersistentState state)
        {
            if (state == PersistentState.Deleted ||
                state == PersistentState.Changed ||
                state == PersistentState.Virtual ||
                state == PersistentState.Original) throw new ArgumentOutOfRangeException("state");
        }

        public ValueObject(IDataMapper mapper) : base(mapper) { }

        public ValueObject(IDataMapper mapper, PersistentState state) : base(mapper)
        {
            this.CheckState(state);
            this.state = state; // PersistentState.Loading || PersistentState.New
        }
    }
}