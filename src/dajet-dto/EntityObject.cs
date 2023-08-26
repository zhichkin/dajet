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
    public class EntityObject : PersistentObject, IComparable
    {
        public static EntityObject Empty { get; } = new(null, 0, Guid.Empty);

        protected int _typeCode;
        protected Guid _identity;
        protected string _name = string.Empty;
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
        public string Name { set { Set(value, ref _name); } get { return Get(ref _name); } }
        public bool IsEmpty() { return _typeCode == 0 && _identity == Guid.Empty; }
        public override string ToString() { return _name; }
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
        public virtual int CompareTo(object target)
        {
            if (target is not EntityObject test)
            {
                throw new InvalidOperationException();
            }

            return CompareTo(test);
        }
        public virtual int CompareTo(EntityObject target)
        {
            if (target is null) { return 1; }

            if (GetType() != target.GetType())
            {
                throw new InvalidOperationException();
            }

            if (_typeCode == target._typeCode)
            {
                return _name.CompareTo(target._name);
            }

            return _typeCode.CompareTo(target._typeCode);
        }
    }
}