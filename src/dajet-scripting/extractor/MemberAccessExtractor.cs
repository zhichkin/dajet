using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class MemberAccessExtractor : IScriptWalker
    {
        private readonly Dictionary<string, MemberAccessExpression> _expressions = new();
        public List<MemberAccessExpression> Extract(in SyntaxNode node)
        {
            ScriptWalker.Walk(in node, this);

            return _expressions.Values.ToList();
        }
        public void SayHello(in SyntaxNode node)
        {
            if (node is MemberAccessExpression expression)
            {
                if (!_expressions.ContainsKey(expression.Identifier))
                {
                    _expressions.Add(expression.Identifier, expression);
                }
            }
        }
        public void SayGoodbye(in SyntaxNode node)
        {
            // do nothing
        }
    }
}