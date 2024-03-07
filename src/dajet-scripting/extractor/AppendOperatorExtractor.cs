using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class AppendOperatorExtractor : IScriptWalker
    {
        private readonly Dictionary<string, TableJoinOperator> _result = new();
        public List<TableJoinOperator> Extract(in SyntaxNode node)
        {
            //NOTE: the node is traversed bottom-up left-to-right (LR parser)

            ScriptWalker.Walk(in node, this);

            List<TableJoinOperator> list = _result.Values.ToList();

            list.Reverse();

            return list;
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