using DaJet.Data;
using DaJet.Scripting.Model;
using System.Text.Json.Serialization;

namespace DaJet.Scripting
{
    public sealed class GeneratorResult
    {
        public bool Success { get; set; } = true;
        public string Error { get; set; } = string.Empty;
        public string Script { get; set; } = string.Empty;
        [JsonIgnore] public EntityMapper Mapper { get; set; } = new();
        [JsonIgnore] public List<ScriptCommand> Commands { get; set; } = new();
    }
    public sealed class ScriptCommand
    {
        [JsonIgnore] public string Name { get; set; } = string.Empty;
        [JsonIgnore] public string Script { get; set; } = string.Empty;
        [JsonIgnore] public EntityMapper Mapper { get; set; }
        [JsonIgnore] public SyntaxNode Statement { get; set; }
        [JsonIgnore] public Dictionary<string, object> Parameters { get; set; }
    }
}