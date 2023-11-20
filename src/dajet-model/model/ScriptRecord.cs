using DaJet.Data;
using System.Text.Json.Serialization;

namespace DaJet.Model
{
    public sealed class ScriptRecord : EntityObject
    {
        private string _name = string.Empty;
        private string _script = string.Empty; // script source code
        private Entity _owner; // database
        private Entity _parent; // script folder
        private bool _is_folder;
        [JsonPropertyName(nameof(Owner))] public Entity Owner { get { return _owner; } set { Set(value, ref _owner); } }
        [JsonPropertyName(nameof(Parent))] public Entity Parent { get { return _parent; } set { Set(value, ref _parent); } }
        [JsonPropertyName(nameof(Name))] public string Name { get { return _name; } set { Set(value, ref _name); } }
        [JsonPropertyName(nameof(Script))] public string Script { get { return _script; } set { Set(value, ref _script); } }
        [JsonPropertyName(nameof(IsFolder))] public bool IsFolder { get { return _is_folder; } set { Set(value, ref _is_folder); } }
        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? base.ToString() : Name;
        }
    }
}