using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    [Function(UDF_UUIDOF.Name)]
    public sealed class UDF_UUIDOF : IUserDefinedFunction
    {
        public const string Name = "UUIDOF";
        private DatabaseProvider _target;
        public Type ReturnType { get { return typeof(Guid); } }
        public FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Name != UDF_UUIDOF.Name)
            {
                throw new FormatException($"[UUIDOF] invalid mapping {node.Name}");
            }

            if (node.Parameters.Count == 0)
            {
                throw new FormatException("[UUIDOF] parameter missing");
            }

            if (node.Parameters.Count > 1)
            {
                throw new FormatException("[UUIDOF] too many parameters");
            }

            _target = transpiler.Target;

            FunctionDescriptor descriptor;

            SyntaxNode parameter = node.Parameters[0];

            if (parameter is ColumnReference column)
            {
                descriptor = Transpile(in column, in script);
            }
            else if (parameter is ScalarExpression scalar)
            {
                descriptor = Transpile(in scalar, in script);
            }
            else if (parameter is VariableReference variable)
            {
                descriptor = Transpile(in variable, in script);
            }
            else if (parameter is MemberAccessExpression accessor)
            {
                descriptor = Transpile(in accessor, in script);
            }
            else
            {
                throw new FormatException("[UUIDOF] invalid parameter type");
            }

            if (descriptor is not null)
            {
                descriptor.Node = node;
            }

            return descriptor;
        }
        private FunctionDescriptor Transpile(in ColumnReference column, in StringBuilder script)
        {
            if (column.Mapping is null || column.Mapping.Count == 0)
            {
                throw new FormatException("[UUIDOF] invalid column mapping");
            }

            if (column.Mapping.Count == 1)
            {
                return TranspileEntityProperty(in column, in script);
            }
            
            return TranspileUnionProperty(in column, in script);
        }
        private FunctionDescriptor TranspileEntityProperty(in ColumnReference column, in StringBuilder script)
        {
            if (column.Binding is not MetadataProperty property)
            {
                throw new FormatException("[UUIDOF] invalid column binding");
            }

            if (column.Mapping is null || column.Mapping.Count == 0)
            {
                throw new FormatException("[UUIDOF] invalid column mapping");
            }

            ColumnMapper map = column.Mapping[0];

            if (map.Type != UnionTag.Entity)
            {
                throw new FormatException("[UUIDOF] invalid column type");
            }

            script.Append(map.Name);

            if (!string.IsNullOrEmpty(map.Alias))
            {
                script.Append(" AS ").Append(map.Alias);
            }

            return null;
        }
        private FunctionDescriptor TranspileUnionProperty(in ColumnReference column, in StringBuilder script)
        {
            ColumnMapper map = null;

            for (int i = 0; i < column.Mapping.Count; i++)
            {
                if (column.Mapping[i].Type == UnionTag.Entity)
                {
                    map = column.Mapping[i]; break; // RRef
                }
            }

            if (map is null)
            {
                throw new FormatException("[UUIDOF] invalid column type");
            }

            column.Mapping.Clear();
            column.Mapping.Add(map);

            script.Append(map.Name);
            
            if (!string.IsNullOrEmpty(map.Alias))
            {
                script.Append(" AS ").Append(map.Alias);
            }

            return null;
        }
        private FunctionDescriptor Transpile(in ScalarExpression scalar, in StringBuilder script)
        {
            if (scalar.Token != TokenType.Entity)
            {
                throw new FormatException("[UUIDOF] invalid scalar type");
            }

            Entity entity = Entity.Parse(scalar.Literal);

            string value = ParserHelper.GetUuidHexLiteral(entity.Identity);

            if (_target == DatabaseProvider.SqlServer)
            {
                script.Append($"0x{value}");
            }
            else if (_target == DatabaseProvider.PostgreSql)
            {
                script.Append($"CAST(E'\\\\x{value}' AS bytea)");
            }
            else
            {
                throw new FormatException($"[UUIDOF] unsupported database type {_target}");
            }

            return null;
        }
        private FunctionDescriptor Transpile(in VariableReference variable, in StringBuilder script)
        {
            if (variable.Binding is not Entity)
            {
                throw new FormatException("[UUIDOF] invalid variable type");
            }

            string parameterName = $"@UUIDOF_" + variable.Identifier[1..];

            script.Append(parameterName);

            FunctionDescriptor descriptor = new()
            {
                Target = parameterName,
                ReturnType = ReturnType
            };

            return descriptor;
        }
        private FunctionDescriptor Transpile(in MemberAccessExpression accessor, in StringBuilder script)
        {
            if (accessor.Binding is not Type type)
            {
                throw new FormatException("[UUIDOF] invalid property binding");
            }

            if (type != typeof(Entity))
            {
                throw new FormatException("[UUIDOF] invalid property type");
            }

            string parameterName = $"@UUIDOF_" + accessor.Identifier[1..].Replace('.', '_');

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