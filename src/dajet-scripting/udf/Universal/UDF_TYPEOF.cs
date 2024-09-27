using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting.PostgreSql
{
    [Function(UDF_TYPEOF.Name)]
    public sealed class UDF_TYPEOF : IUserDefinedFunction
    {
        public const string Name = "TYPEOF";
        public Type ReturnType { get { return typeof(int); } }
        public FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in FunctionExpression node, in StringBuilder script)
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

            FunctionDescriptor descriptor;

            SyntaxNode parameter = node.Parameters[0];

            if (parameter is ColumnReference column)
            {
                descriptor = Transpile(in transpiler, in column, in script);
            }
            else if (parameter is ScalarExpression scalar)
            {
                descriptor = Transpile(in transpiler, in scalar, in script);
            }
            else if (parameter is VariableReference variable)
            {
                descriptor = Transpile(in transpiler, in variable, in script);
            }
            else if (parameter is MemberAccessExpression accessor)
            {
                descriptor = Transpile(in transpiler, in accessor, in script);
            }
            else
            {
                throw new FormatException("[TYPEOF] invalid parameter type");
            }

            if (descriptor is not null)
            {
                descriptor.Node = node;
            }

            return descriptor;
        }
        private FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in ColumnReference column, in StringBuilder script)
        {
            if (column.Mapping is null || column.Mapping.Count == 0)
            {
                throw new FormatException("[TYPEOF] invalid column mapping");
            }

            if (column.Mapping.Count == 1)
            {
                return TranspileSingleColumn(in transpiler, in column, in script);
            }
            
            return TranspileMultipleColumn(in transpiler, in column, in script);
        }
        private FunctionDescriptor TranspileSingleColumn(in ISqlTranspiler transpiler, in ColumnReference column, in StringBuilder script)
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

            transpiler.Visit(scalar, in script);

            return null;
        }
        private FunctionDescriptor TranspileMultipleColumn(in ISqlTranspiler transpiler, in ColumnReference column, in StringBuilder script)
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

            transpiler.Visit(column, in script); // _RecorderTRef

            // PostgreSQL: 42846: cannot cast type bytea to integer

            //script.Append("to_number(").Append(map.Name).Append(')');

            //script.Append("CAST(").Append(map.Name).Append(" AS int)");

            //script.Append(map.Name).Append("::int");

            //if (!string.IsNullOrEmpty(map.Alias))
            //{
            //    script.Append(" AS ").Append(map.Alias);
            //}

            return null;
        }
        private FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in ScalarExpression scalar, in StringBuilder script)
        {
            if (scalar.Token != TokenType.Entity)
            {
                throw new FormatException("[TYPEOF] invalid scalar type");
            }

            Entity value = Entity.Parse(scalar.Literal);

            script.Append(value.TypeCode);

            return null;
        }
        private FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in VariableReference variable, in StringBuilder script)
        {
            if (variable.Binding is not Entity)
            {
                throw new FormatException("[TYPEOF] invalid variable type");
            }

            string parameterName = $"@TYPEOF_" + variable.Identifier[1..];

            script.Append(parameterName);

            FunctionDescriptor descriptor = new()
            {
                Target = parameterName,
                ReturnType = ReturnType
            };

            return descriptor;
        }
        private FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in MemberAccessExpression accessor, in StringBuilder script)
        {
            if (accessor.Binding is not Type type)
            {
                throw new FormatException("[TYPEOF] invalid property binding");
            }

            if (type != typeof(Entity))
            {
                throw new FormatException("[TYPEOF] invalid property type");
            }

            string parameterName = $"@TYPEOF_" + accessor.Identifier[1..].Replace('.', '_');

            script.Append(parameterName);

            FunctionDescriptor descriptor = new()
            {
                Target = parameterName,
                ReturnType = ReturnType
            };

            return descriptor;
        }
    }
}