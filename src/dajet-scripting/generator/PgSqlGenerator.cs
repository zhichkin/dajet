using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public sealed class PgSqlGenerator : SqlGenerator
    {
        protected override void Visit(in SelectStatement node, in StringBuilder script)
        {
            if (node.CommonTables is not null)
            {
                script.Append("WITH ");

                if (IsRecursive(node.CommonTables))
                {
                    script.Append("RECURSIVE ");
                }

                Visit(node.CommonTables, in script);
            }

            Visit(node.Select, in script);
        }
        protected override void Visit(in SelectExpression node, in StringBuilder script)
        {
            script.Append("SELECT");

            if (node.Distinct)
            {
                script.Append(" DISTINCT");
            }

            script.AppendLine();

            for (int i = 0; i < node.Select.Count; i++)
            {
                if (i > 0) { script.Append(',').Append(Environment.NewLine); }

                Visit(node.Select[i], in script);
            }

            if (node.From is not null) { Visit(node.From, in script); }
            if (node.Where is not null) { Visit(node.Where, in script); }
            if (node.Group is not null) { Visit(node.Group, in script); }
            if (node.Having is not null) { Visit(node.Having, in script); }
            if (node.Order is not null) { Visit(node.Order, in script); }
            if (node.Top is not null) { Visit(node.Top, in script); }
        }
        protected override void Visit(in TopClause node, in StringBuilder script)
        {
            script.AppendLine().Append("LIMIT ");
            Visit(node.Expression, in script);
        }
        private bool IsRecursive(in CommonTableExpression cte)
        {
            if (IsRecursive(in cte, cte.Expression))
            {
                return true;
            }
            
            if (cte.Next is null) { return false; }
            
            return IsRecursive(cte.Next);
        }
        private bool IsRecursive(in CommonTableExpression cte, in SyntaxNode node)
        {
            if (node is SelectExpression select)
            {
                return IsRecursive(in cte, in select);
            }
            else if (node is TableJoinOperator join)
            {
                return IsRecursive(in cte, in join);
            }
            else if (node is TableUnionOperator union)
            {
                return IsRecursive(in cte, in union);
            }
            else if (node is TableReference table)
            {
                return IsRecursive(in cte, in table);
            }

            return false;
        }
        private bool IsRecursive(in CommonTableExpression cte, in SelectExpression select)
        {
            return IsRecursive(in cte, select.From.Expression);
        }
        private bool IsRecursive(in CommonTableExpression cte, in TableJoinOperator node)
        {
            if (IsRecursive(in cte, node.Expression1))
            {
                return true;
            }

            return IsRecursive(in cte, node.Expression2);
        }
        private bool IsRecursive(in CommonTableExpression cte, in TableUnionOperator node)
        {
            if (node.Expression1 is SelectExpression select1)
            {
                if (IsRecursive(in cte, in select1)) { return true; }
            }
            else if (node.Expression1 is TableUnionOperator union1)
            {
                if (IsRecursive(in cte, in union1)) { return true; }
            }

            if (node.Expression2 is SelectExpression select2)
            {
                return IsRecursive(in cte, in select2);
            }
            else if (node.Expression2 is TableUnionOperator union2)
            {
                return IsRecursive(in cte, in union2);
            }

            return false;
        }
        private bool IsRecursive(in CommonTableExpression cte, in TableReference table)
        {
            return (table.Binding == cte);
        }
        protected override void Visit(in List<ColumnMap> mapping, in StringBuilder script)
        {
            ColumnMap column;

            for (int i = 0; i < mapping.Count; i++)
            {
                column = mapping[i];

                if (i > 0) { script.Append(", "); }

                if (column.Type == UnionTag.String)
                {
                    //TODO: optimize cast - do it only for ColumnExpression of SelectStatement
                    //NOTE: input parameters, as an example for comparison operations,
                    //NOTE: are provided as varchar, but columns are mvarchar
                    script.Append($"CAST({column.Name} AS varchar)");
                }
                else
                {
                    script.Append(column.Name);
                }

                if (!string.IsNullOrEmpty(column.Alias))
                {
                    script.Append(" AS ").Append(column.Alias);
                }
            }
        }
        protected override void Visit(in EnumValue node, in StringBuilder script)
        {
            script.Append($"CAST(E'\\\\x{ScriptHelper.GetUuidHexLiteral(node.Uuid)}' AS bytea)");
        }
        protected override void Visit(in ScalarExpression node, in StringBuilder script)
        {
            if (node.Token == TokenType.Boolean)
            {
                script.Append(node.Literal);
            }
            else if (node.Token == TokenType.DateTime)
            {
                if (DateTime.TryParse(node.Literal, out DateTime datetime))
                {
                    script.Append($"\'{datetime.AddYears(YearOffset):yyyy-MM-ddTHH:mm:ss}\'::timestamp");
                }
                else
                {
                    script.Append(node.Literal);
                }
            }
            else if (node.Token == TokenType.String)
            {
                script.Append($"\'{node.Literal}\'");
            }
            else if (node.Token == TokenType.Uuid)
            {
                script.Append($"CAST(E'\\\\x{ScriptHelper.GetUuidHexLiteral(new Guid(node.Literal))}' AS bytea)");
            }
            else if (node.Token == TokenType.Binary || node.Literal.StartsWith("0x"))
            {
                script.Append($"CAST(E'\\\\{node.Literal.TrimStart('0')}' AS bytea)");
            }
            else // Number
            {
                script.Append(node.Literal);
            }
        }
        protected override void Visit(in FunctionExpression node, in StringBuilder script)
        {
            if (node.Name.ToUpperInvariant() == "ISNULL")
            {
                node.Name = "COALESCE";
            }
            base.Visit(in node, in script);
        }
        protected override void Visit(in TableVariableExpression node, in StringBuilder script)
        {
            script.Append($"CREATE TEMPORARY TABLE {node.Name} AS").AppendLine();

            base.Visit(in node, in script);

            script.Append(';').AppendLine();
        }
        protected override void Visit(in TemporaryTableExpression node, in StringBuilder script)
        {
            script.Append($"CREATE TEMPORARY TABLE {node.Name} AS").AppendLine();

            base.Visit(in node, in script);

            script.Append(';').AppendLine();
        }
    }
}