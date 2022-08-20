using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class BooleanOperatorTransformer : IScriptVisitor
    {
        private readonly ComparisonOperatorTransformer _transformer = new();
        public void SayHello(SyntaxNode node)
        {
            if (node is BooleanUnaryOperator @unary)
            {
                if (@unary.Expression is ComparisonOperator comparison0)
                {
                    SyntaxNode node0 = _transformer.Transform(in comparison0);

                    if (node0 != null)
                    {
                        @unary.Expression = node0;
                    }
                }
            }
            else if (node is BooleanBinaryOperator @binary)
            {
                if (@binary.Expression1 is ComparisonOperator comparison1)
                {
                    SyntaxNode node1 = _transformer.Transform(in comparison1);

                    if (node1 != null)
                    {
                        @binary.Expression1 = node1;
                    }
                }

                if (@binary.Expression2 is ComparisonOperator comparison2)
                {
                    SyntaxNode node2 = _transformer.Transform(in comparison2);

                    if (node2 != null)
                    {
                        @binary.Expression2 = node2;
                    }
                }
            }
            else if (node is BooleanGroupExpression @group)
            {
                if (@group.Expression is ComparisonOperator comparison3)
                {
                    SyntaxNode node3 = _transformer.Transform(in comparison3);

                    if (node3 != null)
                    {
                        @group.Expression = node3;
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