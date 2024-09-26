using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting.PostgreSql
{
    public sealed class TYPEOF_FunctionTranspiler : IUserDefinedFunction
    {
        public Type ReturnType { get { return typeof(int); } }
        public void Transpile(in ISqlTranspiler owner, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Name != "TYPEOF")
            {
                throw new FormatException($"[TYPEOF] invalid mapping {node.Name}");
            }

            if (node.Parameters.Count == 0)
            {
                throw new FormatException("[TYPEOF] parameter missing");
            }

            if (node.Parameters.Count > 1)
            {
                throw new FormatException("[TYPEOF] too many parameters");
            }

            SyntaxNode parameter = node.Parameters[0];

            if (parameter is ColumnReference column)
            {
                Transpile(in owner, in column, in script);
            }
            else if (parameter is ScalarExpression scalar)
            {
                Transpile(in owner, in scalar, in script);
            }
            else if (parameter is VariableReference variable)
            {
                Transpile(in owner, in variable, in script);
            }
            else if (parameter is MemberAccessExpression accessor)
            {
                Transpile(in owner, in accessor, in script);
            }
            else
            {
                throw new FormatException("[TYPEOF] invalid parameter type");
            }
        }
        private void Transpile(in ISqlTranspiler owner, in ColumnReference column, in StringBuilder script)
        {
            if (column.Mapping is null || column.Mapping.Count == 0)
            {
                throw new FormatException("[TYPEOF] invalid column mapping");
            }

            if (column.Mapping.Count == 1)
            {
                TranspileSingleColumn(in owner, in column, in script);
            }
            else
            {
                TranspileMultipleColumn(in owner, in column, in script);
            }
        }
        private void TranspileSingleColumn(in ISqlTranspiler owner, in ColumnReference column, in StringBuilder script)
        {
            if (column.Binding is not MetadataProperty property)
            {
                throw new FormatException("[TYPEOF] invalid column binding");
            }

            if (column.Mapping[0].Type != UnionTag.Entity)
            {
                throw new FormatException("[TYPEOF] invalid column type");
            }

            ScalarExpression scalar = new()
            {
                Token = TokenType.Number,
                Literal = property.PropertyType.TypeCode.ToString()
            };

            owner.Visit(scalar, in script);
        }
        private void TranspileMultipleColumn(in ISqlTranspiler owner, in ColumnReference column, in StringBuilder script)
        {
            ColumnMapper map = null;

            for (int i = 0; i < column.Mapping.Count; i++)
            {
                if (column.Mapping[i].Type == UnionTag.TypeCode)
                {
                    map = column.Mapping[i]; break;
                }
            }

            if (map is null)
            {
                throw new FormatException("[TYPEOF] invalid column type");
            }

            column.Mapping.Clear();
            column.Mapping.Add(map);

            owner.Visit(column, in script);
        }
        private void Transpile(in ISqlTranspiler owner, in ScalarExpression scalar, in StringBuilder script)
        {
            if (scalar.Token != TokenType.Entity)
            {
                throw new FormatException("[TYPEOF] invalid scalar type");
            }

            owner.Visit(scalar, in script);
        }
        private void Transpile(in ISqlTranspiler owner, in VariableReference variable, in StringBuilder script)
        {
            if (variable.Binding is not Entity)
            {
                throw new FormatException("[TYPEOF] invalid variable type");
            }

            script.Append("@TYPEOF_").Append(variable.Identifier[1..]);
        }
        private void Transpile(in ISqlTranspiler owner, in MemberAccessExpression accessor, in StringBuilder script)
        {
            if (accessor.Binding is not Type type)
            {
                throw new FormatException("[TYPEOF] invalid property binding");
            }

            if (type != typeof(Entity))
            {
                throw new FormatException("[TYPEOF] invalid property type");
            }

            script.Append("@TYPEOF_").Append(accessor.GetDbParameterName()[1..]);
        }
    }
}