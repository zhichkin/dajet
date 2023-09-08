using DaJet.Data;
using System.Collections.Generic;

namespace DaJet.Model
{
    public sealed class TreeNodeRecord : EntityObject
    {
        private string _name;
        private bool _is_folder;
        private EntityObject _value;
        private TreeNodeRecord _parent;
        public TreeNodeRecord(IDataSource source) : base(source) { }
        public string Name { get { return Get(ref _name); } set { Set(value, ref _name); } }
        public bool IsFolder { get { return Get(ref _is_folder); } set { Set(value, ref _is_folder); } }
        public EntityObject Value { get { return Get(ref _value); } set { Set(value, ref _value); } }
        public TreeNodeRecord Parent { get { return Get(ref _parent); } set { Set(value, ref _parent); } }
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }
        public IEnumerable<TreeNodeRecord> Children { get; }
    }
}