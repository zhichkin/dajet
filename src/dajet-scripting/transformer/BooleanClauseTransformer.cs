using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class BooleanClauseTransformer : IScriptTransformer
    {
        private readonly ComparisonOperatorTransformer _transformer = new();
        public void Transform(in SyntaxNode node)
        {
            if (node is OnClause on)
            {
                Transform(in on);
            }
            else if (node is WhereClause where)
            {
                Transform(in where);
            }
            else if (node is WhenClause when)
            {
                Transform(in when);
            }
            else if (node is UnaryOperator unary)
            {
                Transform(in unary);
            }
            else if (node is BinaryOperator binary)
            {
                Transform(in binary);
            }
            else if (node is GroupOperator group)
            {
                Transform(in group);
            }
        }
        private void Transform(in OnClause clause)
        {
            if (clause.Expression is ComparisonOperator comparison)
            {
                SyntaxNode node = _transformer.Transform(in comparison);

                if (node is not null)
                {
                    clause.Expression = node;
                }
            }
            else
            {
                Transform(clause.Expression);
            }
        }
        private void Transform(in WhenClause clause)
        {
            if (clause.WHEN is ComparisonOperator comparison)
            {
                SyntaxNode node = _transformer.Transform(in comparison);

                if (node is not null)
                {
                    clause.WHEN = node;
                }
            }
            else
            {
                Transform(clause.WHEN);
            }
        }
        private void Transform(in WhereClause clause)
        {
            if (clause.Expression is ComparisonOperator comparison)
            {
                SyntaxNode node = _transformer.Transform(in comparison);

                if (node is not null)
                {
                    clause.Expression = node;
                }
            }
            else
            {
                Transform(clause.Expression);
            }
        }
        private void Transform(in GroupOperator _operator)
        {
            if (_operator.Expression is ComparisonOperator comparison)
            {
                SyntaxNode node = _transformer.Transform(in comparison);

                if (node is not null)
                {
                    _operator.Expression = node;
                }
            }
            else
            {
                Transform(_operator.Expression);
            }
        }
        private void Transform(in UnaryOperator _operator)
        {
            if (_operator.Expression is ComparisonOperator comparison)
            {
                SyntaxNode node = _transformer.Transform(in comparison);

                if (node is not null)
                {
                    _operator.Expression = node;
                }
            }
            else
            {
                Transform(_operator.Expression);
            }
        }
        private void Transform(in BinaryOperator _operator)
        {
            if (_operator.Expression1 is ComparisonOperator comparison1)
            {
                SyntaxNode node1 = _transformer.Transform(in comparison1);

                if (node1 is not null)
                {
                    _operator.Expression1 = node1;
                }
            }
            else
            {
                Transform(_operator.Expression1);
            }

            if (_operator.Expression2 is ComparisonOperator comparison2)
            {
                SyntaxNode node2 = _transformer.Transform(in comparison2);

                if (node2 is not null)
                {
                    _operator.Expression2 = node2;
                }
            }
            else
            {
                Transform(_operator.Expression2);
            }
        }
    }
}