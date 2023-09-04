using System;
using System.ComponentModel.DataAnnotations;

namespace DaJet
{
    public abstract class EntityObject
    {
        private Guid _identity;
        [Key] public Guid Identity
        {
            get { return _identity; }
            set { _identity = value; }
        }
        public int TypeCode { get; set; }
        public override string ToString() { return _identity.ToString(); }
        public override int GetHashCode() { return _identity.GetHashCode(); }
        public override bool Equals(object target)
        {
            if (target is null) { return false; }

            if (GetType() != target.GetType()) { return false; }

            if (target is not EntityObject test) { return false; }

            return _identity == test._identity;
        }
        public static bool operator ==(EntityObject left, EntityObject right)
        {
            if (ReferenceEquals(left, right)) { return true; }

            if (left is null || right is null) { return false; }

            return left.Equals(right);
        }
        public static bool operator !=(EntityObject left, EntityObject right)
        {
            return !(left == right);
        }
    }
}