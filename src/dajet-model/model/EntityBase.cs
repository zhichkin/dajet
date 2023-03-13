using System;

namespace DaJet.Model
{
    public abstract class EntityBase : ReferenceObject, IComparable
    {
        public EntityBase(IDataMapper mapper) : base(mapper) { }
        public EntityBase(IDataMapper mapper, Guid identity) : base(mapper, identity) { }

        protected string name = string.Empty;
        public string Name { set { Set<string>(value, ref name); } get { return Get<string>(ref name); } }

        private int typeCode = 0;
        public virtual int TypeCode { get { return typeCode; } }

        public override string ToString() { return this.Name; }

        public virtual int CompareTo(object other)
        {
            return this.CompareTo((EntityBase)other);
        }
        public virtual int CompareTo(EntityBase other)
        {
            if (other == null) return 1;
            if (this.GetType() != other.GetType()) throw new InvalidOperationException();
            return this.Name.CompareTo(other.Name);
        }
    }
}