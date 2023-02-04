using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public static class DataTypeInferencer
    {
        public static UnionType Infer(in SyntaxNode node)
        {
            UnionType union = new();

            Visit(in node, ref union);

            return union;
        }
        private static void Visit(in SyntaxNode node, ref UnionType union)
        {
            if (node is ColumnExpression column)
            {
                Visit(in column, ref union);
            }
            else if (node is ColumnReference identifier)
            {
                Visit(in identifier, ref union);
            }
            else if (node is VariableReference variable)
            {
                Visit(in variable, ref union);
            }
            else if (node is ScalarExpression scalar)
            {
                Visit(in scalar, ref union);
            }
            else if (node is FunctionExpression function)
            {
                Visit(in function, ref union);
            }
            else if (node is CaseExpression _case)
            {
                Visit(in _case, ref union);
            }
            else if (node is WhenExpression when)
            {
                Visit(in when, ref union);
            }
        }
        private static void Visit(in ColumnExpression column, ref UnionType union)
        {
            Visit(column.Expression, ref union);
        }
        private static void Visit(in FunctionExpression function, ref UnionType union)
        {
            foreach (SyntaxNode parameter in function.Parameters)
            {
                Visit(in parameter, ref union);
            }
        }
        private static void Visit(in CaseExpression node, ref UnionType union)
        {
            foreach(WhenExpression when in node.CASE)
            {
                Visit(when, ref union);
            }
            Visit(node.ELSE, ref union);
        }
        private static void Visit(in WhenExpression when, ref UnionType union)
        {
            Visit(when.THEN, ref union);
        }
        private static void Visit(in ScalarExpression scalar, ref UnionType union)
        {
            if (scalar.Token == TokenType.Boolean)
            {
                union.IsBoolean = true;
            }
            else if (scalar.Token == TokenType.Number)
            {
                union.IsNumeric = true;
            }
            else if (scalar.Token == TokenType.DateTime)
            {
                union.IsDateTime = true;
            }
            else if (scalar.Token == TokenType.String)
            {
                union.IsString = true;
            }
            else if (scalar.Token == TokenType.Binary)
            {
                union.IsBinary = true;
            }
            else if (scalar.Token == TokenType.Uuid)
            {
                union.IsUuid = true;
            }
            else if (scalar.Token == TokenType.Entity)
            {
                if (Entity.TryParse(scalar.Literal, out Entity entity))
                {
                    union.IsEntity = true;
                    union.TypeCode = entity.TypeCode;
                }
            }
            else if (scalar.Token == TokenType.Version)
            {
                union.IsVersion = true;
            }
            else if (scalar.Token == TokenType.Integer)
            {
                union.IsInteger = true;
            }
            else if (scalar.Token == TokenType.NULL)
            {
                union = new UnionType(); // undefined
            }
        }
        private static void Visit(in ColumnReference identifier, ref UnionType union)
        {
            if (identifier.Tag is MetadataProperty property)
            {
                Visit(in property, ref union);
            }
            else if (identifier.Tag is SyntaxNode node)
            {
                Visit(in node, ref union);
            }
        }
        private static void Visit(in VariableReference identifier, ref UnionType union)
        {
            if (identifier.Tag is Entity entity)
            {
                union.IsEntity = true;
                union.TypeCode = entity.TypeCode;
                return;
            }

            if (identifier.Tag is not Type type)
            {
                return;
            }

            if (type == typeof(Guid))
            {
                union.IsUuid = true;
            }
            else if (type == typeof(bool))
            {
                union.IsBoolean = true;
            }
            else if (type == typeof(decimal))
            {
                union.IsNumeric = true;
            }
            else if (type == typeof(DateTime))
            {
                union.IsDateTime = true;
            }
            else if (type == typeof(string))
            {
                union.IsString = true;
            }
            else if (type == typeof(byte[]))
            {
                union.IsBinary = true;
            }
            else if (type == typeof(ulong))
            {
                union.IsVersion = true;
            }
            else if (type == typeof(int))
            {
                union.IsInteger = true;
            }
        }
        private static void Visit(in MetadataProperty property, ref UnionType union)
        {
            union.Merge(DataMapper.GetUnionType(in property));
        }
    }
}