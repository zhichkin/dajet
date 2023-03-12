using System;

namespace DaJet.Orm
{
    public sealed partial class Relation : ValueObject, IComparable
    {
        private static readonly IDataMapper _mapper = MetadataPersistentContext.Current.GetDataMapper(typeof(Relation));

        public Relation() : base(_mapper) { }
        public Relation(PersistentState state) : base(_mapper, state) { }

        private Property old_property = null;
        private Entity old_entity = null;

        private Property property = null;
        private Entity entity = null;

        public Property Property { set { Set<Property>(value, ref property); } get { return Get<Property>(ref property); } }
        public Entity Entity { set { Set<Entity>(value, ref entity); } get { return Get<Entity>(ref entity); } }

        protected override void UpdateKeyValues()
        {
            old_property = property;
            old_entity = entity;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})",
                this.Property == null ? string.Empty : this.Property.Name,
                this.Entity == null ? string.Empty : this.Entity.FullName);
        }
        public int CompareTo(object other)
        {
            return this.CompareTo((Relation)other);
        }
        public int CompareTo(Relation other)
        {
            
            if (other == null) return 1;
            if (this.Entity == null) return -1;
            if (other.Entity == null) return 1;
            return this.Entity.FullName.CompareTo(other.Entity.FullName);
        }
    }
}