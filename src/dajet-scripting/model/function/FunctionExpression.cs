using System.Text;

namespace DaJet.Scripting.Model
{
    public sealed class FunctionExpression : SyntaxNode
    {
        public string Name { get; set; } = string.Empty;
        public List<SyntaxNode> Parameters { get; set; } = new();
        public OverClause Over { get; set; }
        public TokenType Modifier { get; set; }
        public string GetVariableIdentifier()
        {
            StringBuilder identifier = new();

            identifier.Append('@').Append(Name.Replace('.', '_'));

            for (int i = 0; i < Parameters.Count; i++)
            {
                if (Parameters[i] is VariableReference variable)
                {
                    identifier.Append('_').Append(variable.Identifier[1..]);
                }
            }

            return identifier.ToString().ToLowerInvariant();
        }
    }
}