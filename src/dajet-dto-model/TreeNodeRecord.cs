using System.Reflection;

namespace DaJet.Model
{
    [Entity(10)]
    public sealed class TreeNodeRecord : EntityObject
    {
        private static readonly int MY_TYPE_CODE;
        static TreeNodeRecord()
        {
            EntityAttribute attribute = typeof(TreeNodeRecord).GetCustomAttribute<EntityAttribute>();

            if (attribute is not null)
            {
                MY_TYPE_CODE = attribute.TypeCode;
            }
        }

        private string _name;
        private bool _folder;
        private EntityObject _value;
        private TreeNodeRecord _parent;
        public TreeNodeRecord(IDataSource source) : base(source, MY_TYPE_CODE) { }
        public TreeNodeRecord(IDataSource source, Guid identity) : base(source, MY_TYPE_CODE, identity) { }
        public string Name { set { Set(value, ref _name); } get { return Get(ref _name); } }
        public bool IsFolder { set { Set(value, ref _folder); } get { return Get(ref _folder); } }
        public EntityObject Value { set { Set(value, ref _value); } get { return Get(ref _value); } }
        public TreeNodeRecord Parent { set { Set(value, ref _parent); } get { return Get(ref _parent); } }
        public List<TreeNodeRecord> Children { get; }
        public override string ToString() { return _name; }
    }
}