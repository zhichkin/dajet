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
                //TODO: исключения (смотри описание выше) !!!
            }

            return union;
        }
        private static string _name = string.Empty; // TODO: убрать костыль !
        
        public static UnionType Infer(in SyntaxNode node)
        {
            UnionType union = new();
            Visit(in node, in union);
            return union;
        }
        public static bool TryInfer(in SyntaxNode node, out UnionType union)
        {
            union = new();
            Visit(in node, in union);
            return !union.IsUndefined;
        }

        #region "INFER RETURN DATA TYPE AND MAP COLUMN EXPRESSION FOR USE BY DATA MAPPER"
        public static void Map(in ColumnExpression node, in EntityMapper mapper)
        {
            UnionType type = new();

            Visit(in node, in type);

            mapper.AddPropertyMapper(in _name, in type);

            _name = string.Empty;
        }
        public static bool TryMap(in ColumnExpression node, out string name, out UnionType type)
        {
            type = new UnionType();

            Visit(in node, in type);

            name = _name;

            _name = string.Empty;

            return !string.IsNullOrEmpty(name);
        }
        public static void Visit(in SyntaxNode node, in UnionType union)
        {
            //NOTE: see PropertyMapper CreatePropertyMap(in ColumnExpression source)

            if (node is ColumnExpression column) { Visit(in column, in union); }
            else if (node is ColumnReference identifier) { Visit(in identifier, in union); }
            else if (node is ScalarExpression scalar) { Visit(in scalar, in union); }
            else if (node is VariableReference variable) { Visit(in variable, in union); }
            else if (node is CaseExpression _case) { Visit(in _case, in union); }
            else if (node is FunctionExpression function) { Visit(in function, in union); }
            else if (node is TableExpression table) { Visit(in table, in union); }
            else if (node is MemberAccessExpression member) { Visit(in member, in union); }
            else if (node is GroupOperator group) { Visit(in group, in union); }
            else if (node is UnaryOperator unary) { Visit(in unary, in union); }
            else if (node is AdditionOperator addition) { Visit(in addition, in union); }
            else if (node is MultiplyOperator multiply) { Visit(in multiply, in union); }
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
            string name = function.Name.ToUpperInvariant();

            if (name == "COUNT")
            {
                union.IsInteger = true; return;
            }
            else if (name == "ROW_NUMBER")
            {
                //TODO: IsVersion is int64 (bigint) hack
                //NOTE: the function does not have any parameters
                union.IsVersion = true; return;
            }
            else if (name == "DATALENGTH" || name == "OCTET_LENGTH" || name == "CHARLENGTH")
            {
                //TODO: IsInteger is int32 (int) hack
                //NOTE: the function have one parameter, but we ignore it
                union.IsInteger = true; return;
            }
            else if (name == "SUBSTRING" || name == "STRING_AGG"
                || name == "CONCAT" || name == "CONCAT_WS" || name == "REPLACE"
                || name == "LOWER" || name == "UPPER" || name == "LTRIM" || name == "RTRIM")
            {
                union.IsString = true; return;
            }
            else if (name == "NOW")
            {
                union.IsDateTime = true; return;
            }
            else if (name == "UUIDOF")
            {
                union.IsUuid = true; return;
            }
            else if (name == "TYPEOF")
            {
                union.IsInteger = true; return;
            }
            else if (name == "VECTOR")
            {
                //union.IsNumeric = true; return;
                //TODO: IsVersion is int64 (bigint) hack
                union.IsVersion = true; return;
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
        private static void Visit(in TableExpression table, in UnionType union)
        {
            ColumnExpression column = GetFirstColumnExpression(in table);

            if (column is not null)
            {
                Visit(in column, in union);
            }
        }
        private static void Visit(in MemberAccessExpression member, in UnionType union)
        {
            if (member.Binding is Type type)
            {
                union.Add(in type);
            }
        }
        private static void Visit(in GroupOperator node, in UnionType union)
        {
            Visit(node.Expression, in union);
        }
        private static void Visit(in UnaryOperator node, in UnionType union)
        {
            if (node.Token == TokenType.Minus)
            {
                Visit(node.Expression, in union);
            }
        }
        private static void Visit(in AdditionOperator node, in UnionType union)
        {
            Visit(node.Expression1, in union);
            Visit(node.Expression2, in union);
        }
        private static void Visit(in MultiplyOperator node, in UnionType union)
        {
            Visit(node.Expression1, in union);
            Visit(node.Expression2, in union);
        }
        private static ColumnExpression GetFirstColumnExpression(in SyntaxNode node)
        {
            if (node is SelectExpression select)
            {
                return GetFirstColumnExpression(in select);
            }
            else if (node is TableExpression table)
            {
                return GetFirstColumnExpression(in table);
            }
            else if (node is TableUnionOperator union)
            {
                return GetFirstColumnExpression(in union);
            }
            else
            {
                return null;
            }
        }
        private static ColumnExpression GetFirstColumnExpression(in TableExpression table)
        {
            return GetFirstColumnExpression(table.Expression);
        }
        private static ColumnExpression GetFirstColumnExpression(in SelectExpression select)
        {
            return select.Columns.FirstOrDefault();
        }
        private static ColumnExpression GetFirstColumnExpression(in TableUnionOperator union)
        {
            return GetFirstColumnExpression(union.Expression1);
        }
        #endregion

        public static object GetColumnSource(in SyntaxNode node)
        {
            if (node is SelectExpression select)
            {
                return select;
            }
            else if (node is TableUnionOperator union)
            {
                return union.Expression1;
            }
            else if (node is TableExpression table)
            {
                return GetColumnSource(table.Expression);
            }
            else if (node is CommonTableExpression cte)
            {
                return GetColumnSource(cte.Expression);
            }
            else if (node is TableVariableExpression variable)
            {
                return GetColumnSource(variable.Expression);
            }
            else if (node is TemporaryTableExpression temporary)
            {
                return GetColumnSource(temporary.Expression);
            }
            else if (node is TableReference reference)
            {
                return GetColumnSource(in reference);
            }

            return null;
        }
        public static object GetColumnSource(in TableReference node)
        {
            if (node.Binding is ApplicationObject)
            {
                return node.Binding;
            }
            else if (node.Binding is TableExpression table)
            {
                return GetColumnSource(table.Expression);
            }
            else if (node.Binding is CommonTableExpression cte)
            {
                return GetColumnSource(cte.Expression);
            }
            else if (node.Binding is TableVariableExpression variable)
            {
                return GetColumnSource(variable.Expression);
            }
            else if (node.Binding is TemporaryTableExpression temporary)
            {
                return GetColumnSource(temporary.Expression);
            }
            
            return null;
        }
        public static string GetColumnSourceName(in SyntaxNode node)
        {
            if (node is TableExpression table)
            {
                return table.Alias;
            }
            else if (node is CommonTableExpression cte)
            {
                return cte.Name;
            }
            else if (node is TableVariableExpression variable)
            {
                return variable.Name;
            }
            else if (node is TemporaryTableExpression temporary)
            {
                return temporary.Name;
            }
            else if (node is TableReference reference)
            {
                return string.IsNullOrEmpty(reference.Alias) ? reference.Identifier : reference.Alias;
            }

            return string.Empty;
        }

        public static EntityMapper CreateEntityMap(in SyntaxNode node)
        {
            object source = GetColumnSource(in node);

            if (source is ApplicationObject entity)
            {
                return CreateEntityMap(in entity);
            }
            else if (source is SelectExpression select)
            {
                return CreateEntityMap(in select);
            }
            
            throw new InvalidOperationException("Failed to create entity map from data source");
        }
        public static EntityMapper CreateEntityMap(in ApplicationObject entity)
        {
            if (entity is null) { throw new ArgumentNullException(nameof(entity)); }

            EntityMapper map = new();

            int ordinal = 0;
            PropertyMapper column;
            List<ColumnMapper> columns;
            MetadataProperty property;
            List<MetadataProperty> select = entity.Properties;

            for (int i = 0; i < select.Count; i++)
            {
                property = select[i];
                column = new PropertyMapper();
                Visit(in property, in column);

                columns = column.ColumnSequence;
                for (int ii = 0; ii < columns.Count; ii++)
                {
                    columns[ii].Ordinal = ordinal++;
                }

                map.Properties.Add(column);
            }

            return map;
        }
        public static EntityMapper CreateEntityMap(in SelectExpression source)
        {
            EntityMapper map = new();

            int ordinal = 0;
            PropertyMapper property;
            List<ColumnMapper> columns;
            ColumnExpression column;
            List<ColumnExpression> select = source.Columns;

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
        public static PropertyMapper CreatePropertyMap(in SyntaxNode expression)
        {
            PropertyMapper map = new();
            
            Map(in expression, in map);
            
            //TODO: set property map name during type inference - убрать костыль !!!
            map.Name = _name;
            _name = string.Empty;

            return map;
        }
        public static PropertyMapper CreatePropertyMap(in ColumnExpression source)
        {
            PropertyMapper map = new();

            if (!string.IsNullOrEmpty(source.Alias))
            {
                map.Name = source.Alias;
            }

            //NOTE: void Visit(in SyntaxNode node, in UnionType union)

            if (source.Expression is ColumnReference column) { Visit(in column, in map); }
            else if (source.Expression is CaseExpression case_when_then_else) { Map(case_when_then_else, in map); }
            else if (source.Expression is ScalarExpression scalar) { Map(scalar, in map); }
            else if (source.Expression is VariableReference variable) { Map(variable, in map); }
            else if (source.Expression is FunctionExpression function) { Map(function, in map); }
            else if (source.Expression is MemberAccessExpression member) { Map(member, in map); }
            else if (source.Expression is TableExpression table) { Map(table, in map); }
            else if (source.Expression is GroupOperator group) { Map(group, in map); }
            else if (source.Expression is UnaryOperator unary) { Map(unary, in map); }
            else if (source.Expression is AdditionOperator addition) { Map(addition, in map); }
            else if (source.Expression is MultiplyOperator multiply) { Map(multiply, in map); }
            
            return map;
        }
        private static void Map(in SyntaxNode expression, in PropertyMapper map)
        {
            UnionType type = new();
            
            Visit(in expression, in type);

            map.DataType.Merge(type);

            List<UnionTag> columns = type.ToColumnList();

            for (int i = 0; i < columns.Count; i++)
            {
                ColumnMapper column = new()
                {
                    Name = map.Name,
                    Type = columns[i],
                    Alias = type.IsUnion ? $"{map.Name}_{UnionType.GetLiteral(columns[i])}" : map.Name,
                    TypeName = UnionType.GetDbTypeName(columns[i])
                };

                map.Columns.Add(column.Type, column);
            }
        }
        private static void Visit(in ColumnReference node, in PropertyMapper map)
        {
            if (node.Binding is MetadataProperty property)
            {
                Visit(in property, in map);
            }
            else if (node.Binding is ColumnExpression column)
            {
                if (string.IsNullOrEmpty(map.Name))
                {
                    ParserHelper.GetColumnIdentifiers(node.Identifier, out string _, out string columnAlias);

                    map.Name = string.IsNullOrEmpty(column.Alias) ? columnAlias : column.Alias;
                }

                Map(column, in map);
            }
            else if (node.Binding is EnumValue)
            {
                Map(node, in map);
            }
        }
        private static void Visit(in MetadataProperty property, in PropertyMapper map)
        {
            map.IsDbGenerated = property.IsDbGenerated;

            UnionType type = GetUnionType(in property);

            map.DataType.Merge(type);

            if (string.IsNullOrEmpty(map.Name))
            {
                map.Name = property.Name;
            }
            
            foreach (MetadataColumn source in property.Columns)
            {
                ColumnMapper column = new()
                {
                    Name = source.Name,
                    TypeName = GetDbTypeName(in property, source.Purpose),
                    Type = type.IsUnion ? source.Purpose.GetUnionTag() : type.GetSingleTagOrUndefined(),
                    Alias = type.IsUnion ? $"{map.Name}_{source.Purpose.GetLiteral()}" : map.Name
                };
                map.Columns.Add(column.Type, column);
            }
        }

        public static string GetDbTypeName(in MetadataProperty property, ColumnPurpose purpose)
        {
            DataTypeDescriptor type = property.PropertyType;

            if (purpose == ColumnPurpose.Default)
            {
                if (type.IsUuid) { return "binary(16)"; }
                else if (type.IsBinary) { return "varbinary(max)"; }
                else if (type.IsValueStorage) { return "varbinary(max)"; }
                else if (type.CanBeBoolean) { return "binary(1)"; }
                else if (type.CanBeNumeric) { return $"numeric({type.NumericPrecision},{type.NumericScale})"; }
                else if (type.CanBeDateTime) { return "datetime2"; }
                else if (type.CanBeString) { return $"n{(type.StringKind == StringKind.Variable ? "var" : string.Empty)}char({(type.StringLength == 0 ? "max" : type.StringLength.ToString())})"; }
                else if (type.CanBeReference) { return "binary(16)"; }
                else { return property.Columns[0].TypeName; }
            }
            else
            {
                MetadataColumn column = null;
                for (int i = 0; i < property.Columns.Count; i++)
                {
                    column = property.Columns[i];
                    if (column.Purpose == purpose) { break; }
                }

                if (column is null)
                {
                    throw new InvalidOperationException($"Column purpose [{purpose}] is not found for property \"{property.Name}\".");
                }

                if (purpose == ColumnPurpose.Tag || purpose == ColumnPurpose.Boolean) { return "binary(1)"; }
                else if (purpose == ColumnPurpose.Numeric) { return $"numeric({type.NumericPrecision},{type.NumericScale})"; }
                else if (purpose == ColumnPurpose.DateTime) { return $"datetime2"; }
                else if (purpose == ColumnPurpose.String) { return $"n{(type.StringKind == StringKind.Variable ? "var" : string.Empty)}char({(type.StringLength == 0 ? "max" : type.StringLength.ToString())})"; }
                else if (purpose == ColumnPurpose.Binary) { return $"varbinary({(column.Length == -1 ? "max" : column.Length.ToString())})"; }
                else if (purpose == ColumnPurpose.TypeCode) { return $"binary(4)"; }
                else if (purpose == ColumnPurpose.Identity) { return $"binary(16)"; }
            }

            throw new InvalidOperationException($"Failed to get DbTypeName for property \"{property.Name}\".");
        }

        public static List<PropertyMappingRule> CreateMappingRules(in SyntaxNode target, in SyntaxNode source, in List<SetExpression> mapping)
        {
            List<PropertyMappingRule> rules = new();

            EntityMapper target_map = CreateEntityMap(in target);
            EntityMapper source_map = CreateEntityMap(in source);

            if (mapping is not null) // update set clause mapping
            {
                foreach (SetExpression set in mapping)
                {
                    for (int i = 0; i < target_map.Properties.Count; i++)
                    {
                        PropertyMapper column = target_map.Properties[i];

                        if (set.Column.GetName() == column.Name)
                        {
                            PropertyMappingRule rule = new()
                            {
                                Target = column
                            };

                            PropertyMapper initializer = CreatePropertyMap(set.Initializer);

                            foreach (PropertyMapper source_column in source_map.Properties)
                            {
                                if (initializer.Name == source_column.Name)
                                {
                                    rule.Source = source_column;

                                    rule.Columns = CreateMappingRules(in rule);

                                    rules.Add(rule);

                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else // insert and select mapping
            {
                for (int i = 0; i < target_map.Properties.Count; i++)
                {
                    PropertyMapper property = target_map.Properties[i];

                    PropertyMappingRule rule = new()
                    {
                        Target = property
                    };

                    foreach (PropertyMapper initializer in source_map.Properties)
                    {
                        if (property.Name == initializer.Name)
                        {
                            rule.Source = initializer;

                            break;
                        }
                    }

                    rule.Columns = CreateMappingRules(in rule);

                    rules.Add(rule);
                }
            }

            return rules;
        }
        public static List<ColumnMappingRule> CreateMappingRules(in PropertyMappingRule mapping)
        {
            List<ColumnMappingRule> rules = new();
            List<ColumnMapper> sequence = mapping.Target.ColumnSequence;

            if (mapping.Source is null) // map default values
            {
                foreach (ColumnMapper column in sequence)
                {
                    rules.Add(new ColumnMappingRule()
                    {
                        Target = column,
                        Source = new ScalarExpression()
                        {
                            Token = ParserHelper.GetDataTypeToken(UnionType.GetDataType(column.Type)),
                            Literal = UnionType.GetDefaultValueLiteral(column.Type)
                        }
                    });
                }
                return rules;
            }

            for (int i = 0; i < sequence.Count; i++)
            {
                ColumnMapper target_column = sequence[i];

                ColumnMappingRule rule = new() { Target = target_column };

                if (mapping.Source.TryMapColumn(target_column.Type, out ColumnMapper source_column))
                {
                    rule.Source = source_column; // map column to column
                }
                else if (target_column.Type == UnionTag.Tag)
                {
                    if (mapping.Source.DataType.UseTypeCode || // is union reference type
                        mapping.Source.DataType.IsEntity)      // is single reference type
                    {
                        if (mapping.Target.DataType.Is(UnionTag.Entity)) // target can be entity
                        {
                            rule.Source = new ScalarExpression() // map _TYPE column to default value 0x08 - Entity
                            {
                                Token = ParserHelper.GetDataTypeToken(UnionType.GetDataType(UnionTag.Tag)),
                                Literal = UnionType.GetHexString(UnionTag.Entity)
                            };
                        }
                        else
                        {
                            rule.Source = new ScalarExpression() // map _TYPE column to default value 0x01 - Undefined
                            {
                                Token = ParserHelper.GetDataTypeToken(UnionType.GetDataType(UnionTag.Tag)),
                                Literal = UnionType.GetHexString(UnionTag.Tag)
                            };
                        }
                    }
                    else
                    {
                        UnionTag source_type = mapping.Source.DataType.GetSingleTagOrUndefined();

                        if (mapping.Target.DataType.Is(source_type)) // target can be of source type
                        {

                            rule.Source = new ScalarExpression() // map _TYPE column to default value 0x02...0x05
                            {
                                Token = ParserHelper.GetDataTypeToken(UnionType.GetDataType(UnionTag.Tag)),
                                Literal = UnionType.GetHexString(source_type)
                            };
                        }
                        else
                        {
                            rule.Source = new ScalarExpression() // map _TYPE column to default value 0x01 - Undefined
                            {
                                Token = ParserHelper.GetDataTypeToken(UnionType.GetDataType(UnionTag.Tag)),
                                Literal = UnionType.GetHexString(UnionTag.Tag)
                            };
                        }
                    }
                }
                else if (target_column.Type == UnionTag.TypeCode)
                {
                    if (mapping.Source.DataType.IsEntity) // is single reference type
                    {
                        rule.Source = new ScalarExpression() // map _TRef column to type code constant value
                        {
                            Token = ParserHelper.GetDataTypeToken(UnionType.GetDataType(UnionTag.TypeCode)),
                            Literal = $"0x{Convert.ToHexString(DbUtilities.GetByteArray(mapping.Source.DataType.TypeCode))}"
                        };
                    }
                    else
                    {
                        rule.Source = new ScalarExpression() // map _TRef column to type code default value
                        {
                            Token = ParserHelper.GetDataTypeToken(UnionType.GetDataType(UnionTag.TypeCode)),
                            Literal = UnionType.GetHexString(UnionTag.Undefined)
                        };
                    }
                }
                else // map primitive data type column to default value
                {
                    rule.Source = new ScalarExpression() // map column to default value
                    {
                        Token = ParserHelper.GetDataTypeToken(UnionType.GetDataType(target_column.Type)),
                        Literal = UnionType.GetDefaultValueLiteral(target_column.Type)
                    };
                }

                rules.Add(rule);
            }

            return rules;
        }
    }
}