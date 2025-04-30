using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    [Function(UDF_UUID1C.Name)]
    public sealed class UDF_UUID1C : IUserDefinedFunction
    {
        public const string Name = "UUID1C";
        private DatabaseProvider _target;
        public Type ReturnType { get { return typeof(Guid); } }
        public FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Name != UDF_UUID1C.Name)
            {
                throw new FormatException($"[UUID1C] invalid mapping {node.Name}");
            }

            if (node.Parameters.Count == 0)
            {
                throw new FormatException("[UUID1C] parameter missing");
            }

            if (node.Parameters.Count > 1)
            {
                throw new FormatException("[UUID1C] too many parameters");
            }

            _target = transpiler.Target;

            FunctionDescriptor descriptor;

            SyntaxNode parameter = node.Parameters[0];

            if (parameter is ColumnReference column)
            {
                descriptor = Transpile(in column, in script);
            }
            else
            {
                throw new FormatException($"[UUID1C] invalid parameter type: {parameter}");
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
                throw new FormatException("[UUID1C] invalid column mapping");
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
                throw new FormatException("[UUID1C] invalid column binding");
            }

            if (column.Mapping is null || column.Mapping.Count == 0)
            {
                throw new FormatException("[UUID1C] invalid column mapping");
            }

            ColumnMapper map = column.Mapping[0];

            if (map.Type != UnionTag.Entity)
            {
                throw new FormatException("[UUID1C] invalid column type");
            }

            GenerateSqlCode(in map, in script);

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
                throw new FormatException("[UUID1C] invalid column type");
            }

            column.Mapping.Clear();
            column.Mapping.Add(map);

            GenerateSqlCode(in map, in script);

            return null;
        }
        private void GenerateSqlCode(in ColumnMapper column, in StringBuilder script)
        {
            if (_target == DatabaseProvider.SqlServer)
            {
                script.Append($"CAST(REVERSE(SUBSTRING({column.Name}, 9, 8)) AS binary(8)) + SUBSTRING({column.Name}, 1, 8)");
            }
            else // PostgreSQL
            {
                for (int i = 16; i > 8; i--)
                {
                    if (i < 16) { script.Append(" || "); }

                    script.Append($"SUBSTRING({column.Name}, {i}, 1)");
                }
                script.Append($" || SUBSTRING({column.Name}, 1, 8)");
            }

            if (!string.IsNullOrEmpty(column.Alias))
            {
                script.Append(" AS ").Append(column.Alias);
            }
        }
    }
}