namespace DaJet.Model
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class EntityAttribute : Attribute
    {
        public EntityAttribute(int typeCode)
        {
            TypeCode = typeCode;
        }
        public int TypeCode { get; private set; }
    }
    public abstract class EntityObject : PersistentObject
    {
        protected int _typeCode;
        protected Guid _identity;
        public EntityObject(IDataSource source, int typeCode) : base(source)
        {
            _typeCode = typeCode;
            _identity = Guid.NewGuid();
        }
        public EntityObject(IDataSource source, int typeCode, Guid identity) : base(source)
        {
            _typeCode = typeCode;
            _identity = identity;
            _state = PersistentState.Virtual;
        }
        public int TypeCode { get { return _typeCode; } }
        public Guid Identity { get { return _identity; } }
        public bool IsEmpty() { return _typeCode == 0 && _identity == Guid.Empty; }
        public override int GetHashCode() { return _identity.GetHashCode(); }
        public override bool Equals(object target)
        {
            if (target is null) { return false; }

            if (GetType() != target.GetType()) { return false; }

            if (target is not EntityObject test) { return false; }

            return _typeCode == test._typeCode && _identity == test._identity;
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