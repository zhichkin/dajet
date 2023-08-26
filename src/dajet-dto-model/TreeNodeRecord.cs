namespace DaJet.Model
{
    [Entity(10)]
    public sealed class TreeNodeRecord : EntityObject
    {
        private static readonly int MY_TYPE_CODE = 10;
        
        private bool _folder;
        private EntityObject _value;
        private TreeNodeRecord _parent;
        public TreeNodeRecord(IDataSource source) : base(source, MY_TYPE_CODE) { }
        public TreeNodeRecord(IDataSource source, Guid identity) : base(source, MY_TYPE_CODE, identity) { }
        public bool IsFolder { set { Set(value, ref _folder); } get { return Get(ref _folder); } }
        public EntityObject Value { set { Set(value, ref _value); } get { return Get(ref _value); } }
        public TreeNodeRecord Parent { set { Set(value, ref _parent); } get { return Get(ref _parent); } }
        
        //public List<CatalogEntity> GetChildren() { return _source.Select<CatalogEntity>(_identity); }
    }
}