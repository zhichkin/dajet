using System;

namespace DaJet.Model
{
    public abstract class ReferenceObject : PersistentObject
    {
        protected Guid _identity;
        public Guid Ref { get { return _identity; } }
        public ReferenceObject(IDataMapper mapper) : base(mapper) { _identity = Guid.NewGuid(); }
        public ReferenceObject(IDataMapper mapper, Guid identity) : base(mapper) { _identity = identity; }
        public void SetStateVirtual()
        {
            if (_state == PersistentState.New)
            {
                _state = PersistentState.Virtual;
            }
            else
            {
                throw new InvalidOperationException($"State transition from [{_state}] to [{PersistentState.Virtual}] is not allowed!");
            }
        }
        public override int GetHashCode() { return _identity.GetHashCode(); }
        public override bool Equals(object target)
        {
            if (target == null) { return false; }

            if (GetType() != target.GetType()) { return false; }

            if (target is not ReferenceObject test) { return false; }

            return _identity == test._identity;
        }
        public static bool operator ==(ReferenceObject left, ReferenceObject right)
        {
            if (ReferenceEquals(left, right)) { return true; }

            if (left is null || right is null) { return false; }

            return left.Equals(right);
        }
        public static bool operator !=(ReferenceObject left, ReferenceObject right)
        {
            return !(left == right);
        }
    }
}