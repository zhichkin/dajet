using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class AppendOperatorExtractor : IScriptWalker
    {
        private readonly Dictionary<string, TableJoinOperator> _result = new();
        public List<TableJoinOperator> Extract(in SyntaxNode node)
        {
            ScriptWalker.Walk(in node, this);

            return _result.Values.ToList();
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