using System;

namespace DaJet.Orm
{
    public abstract class ReferenceObject : Persistent, IReferenceObject
    {
        protected Guid identity;
        protected byte[] version = new byte[8];

        private void CheckState(PersistentState state)
        {
            if (state == PersistentState.Deleted ||
                state == PersistentState.Changed ||
                state == PersistentState.Loading ||
                state == PersistentState.Original) throw new ArgumentOutOfRangeException("state");
        }

        public ReferenceObject(IDataMapper mapper) : base(mapper)
        {
            identity = Guid.NewGuid();
        }

        public ReferenceObject(IDataMapper mapper, Guid identity) : base(mapper)
        {
            this.identity = identity;
        }

        public ReferenceObject(IDataMapper mapper, Guid identity, PersistentState state) : this(mapper, identity)
        {
            this.CheckState(state);
            this.state = state; // PersistentState.Virtual || PersistentState.New
        }

        public Guid Identity { get { return this.identity; } }

        public override Int32 GetHashCode() { return identity.GetHashCode(); }

        public override Boolean Equals(Object obj)
        {
            if (obj == null) { return false; }

            if (this.GetType() != obj.GetType()) { return false; }

            ReferenceObject test = obj as ReferenceObject;

            if (test == null) { return false; }

            return identity == test.identity;
        }

        public static Boolean operator ==(ReferenceObject left, ReferenceObject right)
        {
            if (Object.ReferenceEquals(left, right)) { return true; }

            if (((Object)left == null) || ((Object)right == null)) { return false; }

            return left.Equals(right);
        }

        public static Boolean operator !=(ReferenceObject left, ReferenceObject right)
        {
            return !(left == right);
        }
    }
}