using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    internal sealed class VariablesExtractor : IScriptWalker
    {
        private readonly List<string> _variables = new();
        internal List<string> GetVariables(in SyntaxNode node)
        {
            ScriptWalker.Walk(in node, this);

            return _variables;
        }
        public void SayHello(in SyntaxNode node)
        {
            if (node is VariableReference variable)
            {
                if (!_variables.Contains(variable.Identifier))
                {
                    _variables.Add(variable.Identifier[1..]);
                }
            }
        }
        public void SayGoodbye(in SyntaxNode node)
        {
            // do nothing
        }
    }
}