using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class DaJetFunctionExtractor : IScriptWalker
    {
        private readonly Dictionary<string, FunctionExpression> _functions = new();
        public List<FunctionExpression> Extract(in SyntaxNode node)
        {
            ScriptWalker.Walk(in node, this);

            return _functions.Values.ToList();
        }
        public void SayHello(in SyntaxNode node)
        {
            if (node is FunctionExpression function && function.Token == TokenType.UDF)
            {
                string identifier = function.GetVariableIdentifier();

                if (!_functions.ContainsKey(identifier))
                {
                    _functions.Add(identifier, function);
                }
            }
        }
        public void SayGoodbye(in SyntaxNode node)
        {
            // do nothing
        }
    }
}