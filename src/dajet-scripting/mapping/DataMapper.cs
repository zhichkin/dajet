using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

// Исключения из правил:
// - _KeyField (табличная часть) binary(4) -> int CanBeNumeric
// - _Folder (иерархические ссылочные типы) binary(1) -> bool инвертировать !!!
// - _Version (ссылочные типы) timestamp binary(8) -> IsBinary
// - _Type (тип значений характеристики) varbinary(max) -> IsBinary nullable
// - _RecordKind (вид движения накопления) numeric(1) CanBeNumeric Приход = 0, Расход = 1
// - _DimHash numeric(10) ?

// NOTE: SQL Server rowversion is unsigned big-endian value
// NOTE: 1C binary(4) is integer, unsigned big-endian value

namespace DaJet.Scripting
{
    public static class DataMapper
    {
        public static UnionType GetUnionType(in MetadataProperty property)
        {
            UnionType union = property.PropertyType.GetUnionType();

            foreach (MetadataColumn column in property.Columns)
            {
                if (column.Purpose == ColumnPurpose.Tag)
                {
                    union.UseTag = true;
                    
                    break;
                }
                //TODO: исключения !!!
            }

            return union;
        }
        private static string _name = string.Empty; // TODO: убрать костыль !
        public static void Map(in ColumnExpression node, in EntityMap map)
        {
            UnionType type = new();

            Visit(in node, in type);

            map.Map(in _name, in type);

            _name = string.Empty;
        }
        public static void Visit(in SyntaxNode node, in UnionType union)
        {
            if (node is ColumnExpression column)
            {
                Visit(in column, in union);
            }
            else if (node is ColumnReference identifier)
            {
                Visit(in identifier, in union);
            }
            else if (node is ScalarExpression scalar)
            {
                Visit(in scalar, in union);
            }
            else if (node is VariableReference variable)
            {
                Visit(in variable, in union);
            }
            else if (node is CaseExpression _case)
            {
                Visit(in _case, in union);
            }
            else if (node is FunctionExpression function)
            {
                Visit(in function, in union);
            }
        }
        private static void Visit(in ColumnExpression column, in UnionType union)
        {
            Visit(column.Expression, in union);

            if (!string.IsNullOrEmpty(column.Alias))
            {
                _name = column.Alias;
            }
        }
        private static void Visit(in ColumnReference column, in UnionType union)
        {
            if (column.Binding is MetadataProperty source)
            {
                Visit(in source, in union);
            }
            else if (column.Binding is ColumnExpression parent)
            {
                Visit(in parent, in union);
            }
            else if (column.Binding is EnumValue)
            {
                union.IsUuid = true;
            }
        }
        private static void Visit(in ScalarExpression scalar, in UnionType union)
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
                union.Clear(); // undefined
            }
        }
        private static void Visit(in VariableReference identifier, in UnionType union)
        {
            if (identifier.Binding is Entity entity)
            {
                union.IsEntity = true;
                union.TypeCode = entity.TypeCode;
                return;
            }

            if (identifier.Binding is not Type type)
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
        private static void Visit(in CaseExpression node, in UnionType union)
        {
            foreach (WhenClause when in node.CASE)
            {
                Visit(when.THEN, in union); //NOTE: WHEN clause is not used for type inference
            }

            if (node.ELSE is not null)
            {
                Visit(node.ELSE, in union);
            }
        }
        private static void Visit(in FunctionExpression function, in UnionType union)
        {
            if (function.Name == "ROW_NUMBER")
            {
                //TODO: IsVersion is an int64 (bigint) hack
                //NOTE: the function does not have any parameters
                union.IsVersion = true; return;
            }
            else if (function.Name == "SUBSTRING")
            {
                union.IsString = true; return;
            }

            foreach (SyntaxNode parameter in function.Parameters)
            {
                Visit(in parameter, in union);
            }
        }
        private static void Visit(in MetadataProperty property, in UnionType union)
        {
            _name = property.Name;
            union.Merge(GetUnionType(in property));
        }

        public static EntityMap CreateEntityMap(in SelectExpression source)
        {
            EntityMap map = new();

            int ordinal = 0;
            PropertyMap property;
            List<ColumnMap> columns;
            ColumnExpression column;
            List<ColumnExpression> select = source.Select;

            for (int i = 0; i < select.Count; i++)
            {
                column = select[i];

                property = CreatePropertyMap(in column);

                columns = property.ColumnSequence;

                for (int ii = 0; ii < columns.Count; ii++)
                {
                    columns[ii].Ordinal = ordinal++;
                }

                map.Properties.Add(property);
            }

            return map;
        }
        private static PropertyMap CreatePropertyMap(in ColumnExpression source)
        {
            PropertyMap map = new();

            if (!string.IsNullOrEmpty(source.Alias))
            {
                map.Name = source.Alias;
            }

            if (source.Expression is ColumnReference column)
            {
                Visit(in column, in map);
            }
            else if (source.Expression is CaseExpression case_when_then_else)
            {
                Map(case_when_then_else, in map);
            }
            else if (source.Expression is ScalarExpression scalar)
            {
                Map(scalar, in map);
            }
            else if (source.Expression is VariableReference variable)
            {
                Map(variable, in map);
            }
            else if (source.Expression is FunctionExpression function)
            {
                Map(function, in map);
            }

            return map;
        }
        private static void Map(in SyntaxNode expression, in PropertyMap map)
        {
            UnionType type = new();
            DataMapper.Visit(expression, in type);
            List<UnionTag> columns = type.ToColumnList();

            for (int i = 0; i < columns.Count; i++)
            {
                ColumnMap column = new()
                {
                    Name = map.Name,
                    Type = columns[i],
                    Alias = type.IsUnion ? $"{map.Name}_{UnionType.GetLiteral(columns[i])}" : map.Name,
                    TypeName = UnionType.GetDbTypeName(columns[i])
                };
                map.Columns.Add(column.Type, column);
            }
        }
        private static void Visit(in ColumnReference node, in PropertyMap map)
        {
            if (node.Binding is MetadataProperty property)
            {
                Visit(in property, in map);
            }
            else if (node.Binding is ColumnExpression column)
            {
                if (string.IsNullOrEmpty(map.Name))
                {
                    ScriptHelper.GetColumnIdentifiers(node.Identifier, out string _, out string columnAlias);

                    map.Name = string.IsNullOrEmpty(column.Alias) ? columnAlias : column.Alias;
                }

                Map(column, in map);
            }
            else if (node.Binding is EnumValue)
            {
                Map(node, in map);
            }
        }
        private static void Visit(in MetadataProperty property, in PropertyMap map)
        {
            UnionType type = GetUnionType(in property);

            map.DataType.Merge(type);

            if (string.IsNullOrEmpty(map.Name))
            {
                map.Name = property.Name;
            }
            
            foreach (MetadataColumn source in property.Columns)
            {
                ColumnMap column = new()
                {
                    Name = source.Name,
                    TypeName = SQLHelper.GetDbTypeName(in property, source.Purpose),
                    Type = type.IsUnion ? UnionType.GetPurposeUnionTag(source.Purpose) : type.GetSingleTagOrUndefined(),
                    Alias = type.IsUnion ? $"{map.Name}_{UnionType.GetPurposeLiteral(source.Purpose)}" : map.Name
                };
                map.Columns.Add(column.Type, column);
            }
        }
    }
}