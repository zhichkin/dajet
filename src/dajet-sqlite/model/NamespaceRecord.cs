using DaJet.Data;
using System.Text.Json.Serialization;

namespace DaJet.Sqlite
{
    public sealed class NamespaceRecord : EntityObject
    {
        private string _name = string.Empty;
        private Entity _parent = Entity.Undefined; // namespace
        [JsonPropertyName(nameof(Name))] public string Name { get { return _name; } set { Set(value, ref _name); } }
        [JsonPropertyName(nameof(Parent))] public Entity Parent { get { return _parent; } set { Set(value, ref _parent); } }
        public override string ToString() { return Name; }
    }
}