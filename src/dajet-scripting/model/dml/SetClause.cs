using System.Text.Json.Serialization;

namespace DaJet.Scripting.Model
{
    public sealed class SetClause : SyntaxNode
    {
        public SetClause() { Token = TokenType.SET; }
        [JsonIgnore] public SyntaxNode Parent { get; set; }
        public List<SetExpression> Expressions { get; set; } = new();
    }
}