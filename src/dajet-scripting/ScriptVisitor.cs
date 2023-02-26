using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public abstract class ScriptVisitor
    {
        private void Visit(in SyntaxNode node)
        {
            if (node is ScriptModel script) { Visit(in script); }
            else if (node is CommentStatement comment) { Visit(in comment); }
            else if (node is DeclareStatement declare) { Visit(in declare); }
            else if (node is TypeIdentifier type) { Visit(in type); }
            else if (node is ScalarExpression scalar) { Visit(in scalar); }
            else if (node is VariableReference variable) { Visit(in variable); }
            else if (node is GroupOperator group) { Visit(in group); }
            else if (node is UnaryOperator unary) { Visit(in unary); }
            else if (node is BinaryOperator binary) { Visit(in binary); }
            else if (node is MultiplyOperator multiply) { Visit(in multiply); }
            else if (node is AdditionOperator addition) { Visit(in addition); }
            else if (node is ComparisonOperator comparison) { Visit(in comparison); }
            else if (node is CaseExpression case_when_then_else) { Visit(in case_when_then_else); }
            else if (node is WhenClause when) { Visit(in when); }
            else if (node is FunctionExpression function) { Visit(in function); }
            else if (node is OverClause over) { Visit(in over); }
            else if (node is PartitionClause partition) { Visit(in partition); }
            else if (node is WindowFrame frame) { Visit(in frame); }
            else if (node is FromClause from) { Visit(in from); }
            else if (node is GroupClause group_by) { Visit(in group_by); }
            else if (node is HavingClause having) { Visit(in having); }
            else if (node is OnClause join_on) { Visit(in join_on); }
            else if (node is OrderClause order_by) { Visit(in order_by); }
            else if (node is TopClause top) { Visit(in top); }
            else if (node is WhereClause where) { Visit(in where); }
            else if (node is ColumnExpression column) { Visit(in column); }
            else if (node is ColumnReference reference) { Visit(in reference); }
            else if (node is CommonTableExpression cte) { Visit(in cte); }
            else if (node is SelectExpression select) { Visit(in select); }
            else if (node is SelectStatement select_statement) { Visit(in select_statement); }
            else if (node is StarExpression star) { Visit(in star); }
            else if (node is TableExpression derived) { Visit(in derived); }
            else if (node is TableJoinOperator join) { Visit(in join); }
            else if (node is TableReference table) { Visit(in table); }
            else if (node is TableUnionOperator union) { Visit(in union); }
        }
        protected virtual void Visit(in ScriptModel node)
        {
            foreach (SyntaxNode statement in node.Statements)
            {
                if (statement is SelectStatement select)
                {
                    Visit(in select);
                }
            }
        }
        protected virtual void Visit(in ApplicationObject entity) { }
        protected virtual void Visit(in MetadataProperty property) { }
        protected virtual void Visit(in MetadataColumn column) { }
        protected virtual void Visit(in CommentStatement node) { }
        protected virtual void Visit(in DeclareStatement node)
        {
            Visit(node.Initializer);
        }
        protected virtual void Visit(in TypeIdentifier node) { }
        protected virtual void Visit(in ScalarExpression node) { }
        protected virtual void Visit(in VariableReference node) { }
        protected virtual void Visit(in SelectStatement node)
        {
            if (node.CommonTables is not null)
            {
                Visit(node.CommonTables);
            }
            Visit(node.Select);
        }
        protected virtual void Visit(in SelectExpression node)
        {
            if (node.Top is not null)
            {
                Visit(node.Top);
            }

            for (int i = 0; i < node.Select.Count; i++)
            {
                Visit(node.Select[i]);
            }

            if (node.From is not null) { Visit(node.From); }
            if (node.Where is not null) { Visit(node.Where); }
            if (node.Group is not null) { Visit(node.Group); }
            if (node.Having is not null) { Visit(node.Having); }
            if (node.Order is not null) { Visit(node.Order); }
        }
        protected virtual void Visit(in TableReference node)
        {
            if (node.Binding is ApplicationObject entity)
            {
                Visit(in entity);
            }
            else if (node.Binding is TableExpression table)
            {
                Visit(in table);
            }
            else if (node.Binding is CommonTableExpression cte)
            {
                Visit(in cte);
            }
        }
        protected virtual void Visit(in StarExpression node) { }
        protected virtual void Visit(in ColumnReference node)
        {
            if (node.Binding is MetadataProperty source)
            {
                Visit(in source);
            }
            else if (node.Binding is MetadataColumn column)
            {
                Visit(in column);
            }
            else if (node.Binding is ColumnExpression parent)
            {
                Visit(in parent);
            }
        }
        protected virtual void Visit(in ColumnExpression node)
        {
            Visit(node.Expression);
        }
        protected virtual void Visit(in TableExpression node)
        {
            Visit(node.Expression);
        }
        protected virtual void Visit(in TableJoinOperator node)
        {
            Visit(node.Expression1);
            Visit(node.Expression2);
            Visit(node.On);
        }
        protected virtual void Visit(in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select1)
            {
                Visit(in select1);
            }
            else if (node.Expression1 is TableUnionOperator union1)
            {
                Visit(in union1);
            }
            if (node.Expression2 is SelectExpression select2)
            {
                Visit(in select2);
            }
            else if (node.Expression2 is TableUnionOperator union2)
            {
                Visit(in union2);
            }
        }
        protected virtual void Visit(in CommonTableExpression node)
        {
            if (node.Next is not null)
            {
                Visit(node.Next);
            }
            Visit(node.Expression);
        }
        protected virtual void Visit(in TopClause node)
        {
            Visit(node.Expression);
        }
        protected virtual void Visit(in FromClause node)
        {
            Visit(node.Expression);
        }
        protected virtual void Visit(in WhereClause node)
        {
            Visit(node.Expression);
        }
        protected virtual void Visit(in GroupClause node)
        {
            for (int i = 0; i < node.Expressions.Count; i++)
            {
                Visit(node.Expressions[i]);
            }
        }
        protected virtual void Visit(in HavingClause node)
        {
            Visit(node.Expression);
        }
        protected virtual void Visit(in OnClause node)
        {
            Visit(node.Expression);
        }
        protected virtual void Visit(in OrderClause node)
        {
            for (int i = 0; i < node.Expressions.Count; i++)
            {
                Visit(node.Expressions[i]);
            }

            if (node.Offset is not null)
            {
                Visit(node.Offset);

                if (node.Fetch is not null)
                {
                    Visit(node.Fetch);
                }
            }
        }
        protected virtual void Visit(in GroupOperator node)
        {
            Visit(node.Expression);
        }
        protected virtual void Visit(in UnaryOperator node)
        {
            Visit(node.Expression);
        }
        protected virtual void Visit(in BinaryOperator node)
        {
            Visit(node.Expression1);
            Visit(node.Expression2);
        }
        protected virtual void Visit(in AdditionOperator node)
        {
            Visit(node.Expression1);
            Visit(node.Expression2);
        }
        protected virtual void Visit(in MultiplyOperator node)
        {
            Visit(node.Expression1);
            Visit(node.Expression2);
        }
        protected virtual void Visit(in ComparisonOperator node)
        {
            Visit(node.Expression1);
            Visit(node.Expression2);
        }
        protected virtual void Visit(in CaseExpression node)
        {
            foreach (WhenClause when in node.CASE)
            {
                Visit(when);
            }

            if (node.ELSE is not null)
            {
                Visit(node.ELSE);
            }
        }
        protected virtual void Visit(in WhenClause node)
        {
            Visit(node.WHEN);
            Visit(node.THEN);
        }
        protected virtual void Visit(in FunctionExpression node)
        {
            for (int i = 0; i < node.Parameters.Count; i++)
            {
                Visit(node.Parameters[i]);
            }

            if (node.Over is not null)
            {
                Visit(node.Over);
            }
        }
        protected virtual void Visit(in OverClause node)
        {
            if (node.Partition is not null)
            {
                Visit(node.Partition);
            }
            if (node.Order is not null)
            {
                Visit(node.Order);
            }
            if (node.Preceding is not null || node.Following is not null)
            {
                if (node.Preceding is not null && node.Following is not null)
                {
                    Visit(node.Preceding);
                    Visit(node.Following);
                }
                else if (node.Preceding is not null)
                {
                    Visit(node.Preceding);
                }
            }
        }
        protected virtual void Visit(in WindowFrame node) { }
        protected virtual void Visit(in PartitionClause node)
        {
            for (int i = 0; i < node.Columns.Count; i++)
            {
                Visit(node.Columns[i]);
            }
        }
    }
}