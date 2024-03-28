using DaJet.Data;
using System.Text.Json.Serialization;

namespace DaJet.Sqlite
{
    public sealed class EntityRecord : EntityObject
    {
        private int _type; // discriminator { enum, entity, table }
        private int _code; // entity type code
        private string _name = string.Empty;
        private Entity _parent = Entity.Undefined; // { namespace, entity }
        [JsonPropertyName(nameof(Type))] public int Type { get { return _type; } set { Set(value, ref _type); } }
        [JsonPropertyName(nameof(Code))] public int Code { get { return _code; } set { Set(value, ref _code); } }
        [JsonPropertyName(nameof(Name))] public string Name { get { return _name; } set { Set(value, ref _name); } }
        [JsonPropertyName(nameof(Parent))] public Entity Parent { get { return _parent; } set { Set(value, ref _parent); } }
        public override string ToString() { return string.Format("[{0}] ({1}) {2}", Type, Code, Name); }
    }
}