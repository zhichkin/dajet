using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class TypeInferencer
    {
        public UnionType InferDataType(in SyntaxNode node)
        {
            UnionType union = new();

            Visit(in node, ref union);

            //if (union is not null)
            //{
            //    return MapToType(in union) ?? typeof(decimal);
            //}

            return union;
        }
        private Type MapToType(in UnionType union)
        {
            if (union.IsUnion)
            {
                return typeof(Union);
            }
            else if (union.IsBoolean)
            {
                return typeof(bool);
            }
            else if (union.IsNumeric)
            {
                return typeof(decimal);
            }
            else if (union.IsDateTime)
            {
                return typeof(DateTime);
            }
            else if (union.IsString)
            {
                return typeof(string);
            }
            else if (union.IsUuid)
            {
                return typeof(Guid);
            }
            else if (union.IsBinary)
            {
                return typeof(byte[]);
            }
            else if (union.IsEntity)
            {
                return typeof(Entity);
            }

            return null;
        }
        public object GetDefaultValue(in Type type)
        {
            if (type == typeof(bool))
            {
                return false;
            }
            else if (type == typeof(decimal))
            {
                return 0.0M;
            }
            else if (type == typeof(DateTime))
            {
                return new DateTime(1, 1, 1);
            }
            else if (type == typeof(string))
            {
                return string.Empty;
            }
            else if (type == typeof(byte[]))
            {
                return Array.Empty<byte>();
            }
            else if (type == typeof(Entity))
            {
                return Entity.Undefined;
            }
            else if (type == typeof(Guid))
            {
                return Guid.Empty;
            }

            return null;
        }
        private void Visit(in SyntaxNode node, ref UnionType union)
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
        private void Visit(in ColumnExpression column, ref UnionType union)
        {
            Visit(column.Expression, ref union);
        }
        private void Visit(in FunctionExpression function, ref UnionType union)
        {
            foreach (SyntaxNode parameter in function.Parameters)
            {
                Visit(in parameter, ref union);
            }
        }
        private void Visit(in CaseExpression node, ref UnionType union)
        {
            foreach(WhenExpression when in node.CASE)
            {
                Visit(when, ref union);
            }
            Visit(node.ELSE, ref union);
        }
        private void Visit(in WhenExpression when, ref UnionType union)
        {
            Visit(when.THEN, ref union);
        }
        private void Visit(in ScalarExpression scalar, ref UnionType union)
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
            else if (scalar.Token == TokenType.NULL)
            {
                // do nothing
            }
        }
        private void Visit(in ColumnReference identifier, ref UnionType union)
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
        private void Visit(in VariableReference identifier, ref UnionType union)
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
        }
        private void Visit(in MetadataProperty property, ref UnionType union)
        {
            union.Merge(property.PropertyType.GetUnionType());

            foreach (MetadataColumn column in property.Columns)
            {
                if (column.Purpose == ColumnPurpose.Tag)
                {
                    union.HasTag = true; break;
                }
            }
        }
    }
}