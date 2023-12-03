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
        [JsonIgnore] public List<ScriptStatement> Statements { get; set; } = new();
    }
    public sealed class ScriptStatement
    {
        [JsonIgnore] public SyntaxNode Node { get; set; }
        [JsonIgnore] public string Script { get; set; } = string.Empty; // SQL script code
        [JsonIgnore] public EntityMapper Mapper { get; set; } // SELECT, CONSUME, OUTPUT
    }
}