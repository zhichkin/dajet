using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class WhereOnClauseTransformer : IScriptVisitor
    {
        private readonly ComparisonOperatorTransformer _transformer = new();
        public void SayHello(SyntaxNode node)
        {
            if (node is WhereClause clause1)
            {
                if (clause1.Expression is ComparisonOperator comparison1)
                {
                    SyntaxNode node1 = _transformer.Transform(in comparison1);

                    if (node1 != null)
                    {
                        clause1.Expression = node1;
                    }
                }
            }
            else if (node is OnClause clause2)
            {
                if (clause2.Expression is ComparisonOperator comparison2)
                {
                    SyntaxNode node2 = _transformer.Transform(in comparison2);

                    if (node2 != null)
                    {
                        clause2.Expression = node2;
                    }
                }
            }
        }
        public void SayGoodbye(SyntaxNode node)
        {
            return; // not implemented
        }
    }
}