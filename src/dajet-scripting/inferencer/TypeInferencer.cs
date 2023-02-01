using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class TypeInferencer
    {
        private DataTypeSet _union;
        public DataTypeSet DataType { get { return _union; } }
        public Type InferOrDefault(in SyntaxNode node)
        {
            DataTypeSet union = new();

            VisitSyntaxNode(in node, in union);

            _union = union;

            if (union is not null)
            {
                return MapToType(in union) ?? typeof(decimal);
            }

            return typeof(decimal);
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

            return null;
        }
        private Type MapToType(in DataTypeSet union)
        {
            //TODO: type addition is not supported yet
            //if (union.IsMultipleType)
            //{
            //    return typeof(Union);
            //}
            if (union.CanBeBoolean)
            {
                return typeof(bool);
            }
            else if (union.CanBeNumeric)
            {
                return typeof(decimal);
            }
            else if (union.CanBeDateTime)
            {
                return typeof(DateTime);
            }
            else if (union.CanBeString)
            {
                return typeof(string);
            }
            else if (union.CanBeUuid)
            {
                return typeof(Guid);
            }
            else if (union.CanBeValueStorage)
            {
                return typeof(byte[]);
            }
            else if (union.CanBeReference)
            {
                return typeof(Entity);
            }

            return null;
        }
        private void VisitSyntaxNode(in SyntaxNode node, in DataTypeSet union)
        {
            if (node is ColumnExpression column)
            {
                Visit(in column, in union);
            }
            else if (node is ColumnReference identifier)
            {
                Visit(in identifier, in union);
            }
            else if (node is VariableReference variable)
            {
                Visit(in variable, in union);
            }
            else if (node is ScalarExpression scalar)
            {
                Visit(in scalar, in union);
            }
            else if (node is FunctionExpression function)
            {
                Visit(in function, in union);
            }
            else if (node is CaseExpression _case)
            {
                Visit(in _case, in union);
            }
            else if (node is WhenExpression when)
            {
                Visit(in when, in union);
            }
        }
        private void Visit(in ColumnExpression column, in DataTypeSet union)
        {
            VisitSyntaxNode(column.Expression, in union);
        }
        private void Visit(in FunctionExpression function, in DataTypeSet union)
        {
            foreach (SyntaxNode parameter in function.Parameters)
            {
                VisitSyntaxNode(in parameter, in union);
            }
        }
        private void Visit(in CaseExpression node, in DataTypeSet union)
        {
            foreach(WhenExpression when in node.CASE)
            {
                VisitSyntaxNode(when, in union);
            }
            VisitSyntaxNode(node.ELSE, in union);
        }
        private void Visit(in WhenExpression when, in DataTypeSet union)
        {
            VisitSyntaxNode(when.THEN, in union);
        }
        private void Visit(in ScalarExpression scalar, in DataTypeSet union)
        {
            if (scalar.Token == TokenType.Boolean)
            {
                union.CanBeBoolean = true;
            }
            else if (scalar.Token == TokenType.Number)
            {
                union.CanBeNumeric = true;
            }
            else if (scalar.Token == TokenType.DateTime)
            {
                union.CanBeDateTime = true;
            }
            else if (scalar.Token == TokenType.String)
            {
                union.CanBeString = true;
            }
            else if (scalar.Token == TokenType.Binary)
            {
                //TODO: union.Flags |= DataTypeFlags.Binary;
                union.Identifiers.Add(SingleTypes.ValueStorage);
            }
            else if (scalar.Token == TokenType.Uuid)
            {
                //TODO: union.Flags |= DataTypeFlags.Binary;
                union.Identifiers.Add(SingleTypes.UniqueIdentifier);
            }
            else if (scalar.Token == TokenType.Entity)
            {
                //TODO: add type code to some list !!!

                if (Entity.TryParse(scalar.Literal, out Entity entity))
                {
                    union.TypeCode = entity.TypeCode;
                    union.Reference = entity.Identity;
                    union.CanBeReference = true;
                }
            }
            else if (scalar.Token == TokenType.NULL)
            {
                // do nothing
            }
        }
        private void Visit(in ColumnReference identifier, in DataTypeSet union)
        {
            if (identifier.Tag is MetadataProperty property)
            {
                Visit(in property, in union);
            }
            else if (identifier.Tag is SyntaxNode node)
            {
                VisitSyntaxNode(in node, in union);
            }
        }
        private void Visit(in VariableReference identifier, in DataTypeSet union)
        {
            if (identifier.Tag is Entity entity)
            {
                //TODO: add type code to some list !!!
                union.TypeCode = entity.TypeCode;
                union.Reference = entity.Identity;
                union.CanBeReference = true;
                return;
            }

            if (identifier.Tag is not Type type)
            {
                return;
            }

            if (type == typeof(Guid))
            {
                //TODO: union.Flags |= DataTypeFlags.Binary;
                union.Identifiers.Add(SingleTypes.UniqueIdentifier);
            }
            else if (type == typeof(bool))
            {
                union.CanBeBoolean = true;
            }
            else if (type == typeof(decimal))
            {
                union.CanBeNumeric = true;
            }
            else if (type == typeof(DateTime))
            {
                union.CanBeDateTime = true;
            }
            else if (type == typeof(string))
            {
                union.CanBeString = true;
            }
            else if (type == typeof(byte[]))
            {
                //TODO: union.Flags |= DataTypeFlags.Binary;
                union.Identifiers.Add(SingleTypes.ValueStorage);
            }
        }
        private void Visit(in MetadataProperty property, in DataTypeSet union)
        {
            union.CanBeBoolean = property.PropertyType.CanBeBoolean;
            union.CanBeNumeric = property.PropertyType.CanBeNumeric;
            union.CanBeDateTime = property.PropertyType.CanBeDateTime;
            union.CanBeString = property.PropertyType.CanBeString;
            union.CanBeReference = property.PropertyType.CanBeReference;
            union.TypeCode = property.PropertyType.TypeCode;
            union.Reference = property.PropertyType.Reference;

            if (property.PropertyType.CanBeUuid)
            {
                union.Identifiers.Add(SingleTypes.UniqueIdentifier);
            }
            else if (property.PropertyType.CanBeValueStorage)
            {
                union.Identifiers.Add(SingleTypes.ValueStorage);
            }
        }
    }
}