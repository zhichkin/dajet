using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class AppendOperatorExtractor : IScriptWalker
    {
        private readonly Dictionary<string, TableJoinOperator> _result = new();
        public Dictionary<string, TableJoinOperator> Extract(in SyntaxNode node)
        {
            if (node is null) { return _result; }

            ScriptWalker.Walk(in node, this);

            return _result;
        }
        public void SayHello(in SyntaxNode node)
        {
            if (node is TableJoinOperator join &&
                join.Token == TokenType.APPEND &&
                join.Expression2 is TableExpression table) //right operand
            {
                _result.Add(table.Alias, join);
            }
        }
        public void SayGoodbye(in SyntaxNode node)
        {
            // do nothing
        }
    }
}