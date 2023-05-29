using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class ColumnReferenceTransformer : IScriptTransformer
    {
        public void Transform(in SyntaxNode node)
        {
            if (node is ColumnReference reference)
            {
                Transform(in reference);
            }
            else if (node is ColumnExpression parent && parent.Expression is ColumnReference column)
            {
                Transform(in parent, in column);
            }
        }
        
        private void Transform(in ColumnExpression parent, in ColumnReference column)
        {
            if (column.Binding is MetadataProperty property)
            {
                TransformSourceColumnReference(in parent, in column, in property);
            }
            else if (column.Binding is ColumnExpression)
            {
                TransformDerivedColumnReference(in parent, in column);
            }
        }
        private void TransformSourceColumnReference(in ColumnExpression parent, in ColumnReference column, in MetadataProperty property)
        {
            ScriptHelper.GetColumnIdentifiers(column.Identifier, out string tableAlias, out string columnAlias);

            List<MetadataColumn> columns = property.Columns
                .OrderBy((column) => { return column.Purpose; })
                .ToList();

            column.Mapping = new List<ColumnMap>(columns.Count);

            for (int i = 0; i < columns.Count; i++)
            {
                ColumnMap map = new()
                {
                    Name = string.IsNullOrEmpty(tableAlias)
                        ? columns[i].Name
                        : $"{tableAlias}.{columns[i].Name}",
                    Alias = string.IsNullOrEmpty(parent.Alias)
                        ? property.Name
                        : parent.Alias,
                    Type = (columns.Count > 1)
                        ? UnionType.GetPurposeUnionTag(columns[i].Purpose)
                        : DataMapper.GetUnionType(in property).ToColumnList()[0],
                    TypeName = columns[i].TypeName
                };

                if (columns.Count > 1)
                {
                    map.Alias += $"_{UnionType.GetPurposeLiteral(columns[i].Purpose)}";
                }

                column.Mapping.Add(map);
            }
        }
        private void TransformDerivedColumnReference(in ColumnExpression parent, in ColumnReference column)
        {
            UnionType type = new();
            DataMapper.Visit(parent.Expression, in type);
            List<UnionTag> columns = type.ToColumnList();

            column.Mapping = new List<ColumnMap>(columns.Count);

            for (int i = 0; i < columns.Count; i++)
            {
                ColumnMap map = new()
                {
                    Type = columns[i],
                    Name = column.Identifier,
                    Alias = string.IsNullOrEmpty(parent.Alias)
                        ? string.Empty
                        : parent.Alias
                };

                if (columns.Count > 1)
                {
                    map.Name += $"_{UnionType.GetLiteral(columns[i])}";
                    
                    if (!string.IsNullOrEmpty(map.Alias))
                    {
                        map.Alias += $"_{UnionType.GetLiteral(columns[i])}";
                    }
                }

                column.Mapping.Add(map);
            }
        }

        private void Transform(in ColumnReference column)
        {
            if (column.Mapping is not null) { return; }

            if (column.Binding is MetadataProperty property)
            {
                TransformSourceColumnReference(in column, in property);
            }
            else if (column.Binding is ColumnExpression parent)
            {
                TransformDerivedColumnReference(in column, in parent);
            }
        }
        private void TransformSourceColumnReference(in ColumnReference column, in MetadataProperty property)
        {
            ScriptHelper.GetColumnIdentifiers(column.Identifier, out string tableAlias, out string columnAlias);

            List<MetadataColumn> columns = property.Columns
                .OrderBy((column) => { return column.Purpose; })
                .ToList();

            column.Mapping = new List<ColumnMap>(columns.Count);

            for (int i = 0; i < columns.Count; i++)
            {
                ColumnMap map = new()
                {
                    Name = string.IsNullOrEmpty(tableAlias)
                        ? columns[i].Name
                        : $"{tableAlias}.{columns[i].Name}",
                    Type = (columns.Count > 1)
                        ? UnionType.GetPurposeUnionTag(columns[i].Purpose)
                        : DataMapper.GetUnionType(in property).ToColumnList()[0],
                    TypeName = columns[i].TypeName
                };

                column.Mapping.Add(map);
            }
        }
        private void TransformDerivedColumnReference(in ColumnReference column, in ColumnExpression parent)
        {
            UnionType type = new();
            DataMapper.Visit(parent.Expression, in type);
            List<UnionTag> columns = type.ToColumnList();

            column.Mapping = new List<ColumnMap>(columns.Count);

            for (int i = 0; i < columns.Count; i++)
            {
                ColumnMap map = new()
                {
                    Type = columns[i],
                    Name = column.Identifier
                };

                if (columns.Count > 1)
                {
                    map.Name += $"_{UnionType.GetLiteral(columns[i])}";
                }

                column.Mapping.Add(map);
            }
        }
    }
}