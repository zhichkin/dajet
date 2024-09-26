using DaJet.Data;
using DaJet.Scripting.Model;
using System.Text.Json.Serialization;

namespace DaJet.Scripting
{
    public sealed class SqlStatement
    {
        [JsonIgnore] public SyntaxNode Node { get; set; }
        [JsonIgnore] public string Script { get; set; } = string.Empty; // SQL script code
        [JsonIgnore] public EntityMapper Mapper { get; set; } // SELECT, CONSUME, OUTPUT
        [JsonIgnore] public List<FunctionDescriptor> Functions { get; set; } = new();
    }
}