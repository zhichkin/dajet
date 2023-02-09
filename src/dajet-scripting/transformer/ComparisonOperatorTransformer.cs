using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class ComparisonOperatorTransformer
    {
        public SyntaxNode Transform(in ComparisonOperator comparison)
        {
            // DONE:
            // 1. Ссылка = @Ссылка
            // 2. Ссылка1 = Ссылка2 (составные типы)
            // 3. Ссылка ССЫЛКА Справочник.Номенклатура = <column> IS [NOT] <type>
            // 4. ТИПЗНАЧЕНИЯ(Ссылка) = ТИП(Справочник.Номенклатура) = <column> IS [NOT] <type>
            // 5. <column> IS [NOT] NULL

            if (comparison.Token == TokenType.IS)
            {
                return TransformColumnIsType(in comparison);
            }

            //TODO: refactor union type comparison to use UnionType inference

            if (IsColumnColumn(comparison.Expression1, comparison.Expression2))
            {
                return Transform(comparison, comparison.Expression1, comparison.Expression2);
            }
            else if (IsColumnVariable(comparison.Expression1, comparison.Expression2))
            {
                return Transform(comparison, comparison.Expression1, comparison.Expression2);
            }
            else if (IsVariableColumn(comparison.Expression1, comparison.Expression2))
            {
                return Transform(comparison, comparison.Expression1, comparison.Expression2);
            }
            else if (IsColumnScalar(comparison.Expression1, comparison.Expression2))
            {
                return Transform(comparison, comparison.Expression1, comparison.Expression2);
            }
            else if (IsScalarColumn(comparison.Expression1, comparison.Expression2))
            {
                return Transform(comparison, comparison.Expression1, comparison.Expression2);
            }

            return null; // no transformation is needed
        }

        #region "<union type> == <union type>"
        private bool IsSimpleIsNullOperator(SyntaxNode left, SyntaxNode right)
        {
            return IsScalarColumn(left) && IsNullScalar(right);
        }
        private bool IsNullScalar(SyntaxNode node)
        {
            return (node is ScalarExpression scalar && scalar.Token == TokenType.NULL);
        }
        private bool IsScalarColumn(SyntaxNode node)
        {
            return (node is ColumnReference column
                && column.Binding is MetadataProperty property
                && property.Columns.Count == 1);
        }
        private bool IsUnionColumn(SyntaxNode node, out ColumnReference column)
        {
            column = null;

            if (node is ColumnReference identifier &&
                identifier.Binding is MetadataProperty property &&
                property.Columns.Count > 1)
            {
                column = identifier;
            }

            return (column != null);
        }
        private bool IsTypeIdentifier(SyntaxNode node, out TypeIdentifier type)
        {
            type = null;

            if (node is not TypeIdentifier identifier)
            {
                return false;
            }

            if (identifier.Binding is Type || identifier.Binding is Entity)
            {
                type = identifier;
            }

            return (type != null);
        }
        private bool IsColumnColumn(SyntaxNode node1, SyntaxNode node2)
        {
            return (node1 is ColumnReference column1 &&
                    node2 is ColumnReference column2 &&
                    column1.Binding is MetadataProperty property1 &&
                    column2.Binding is MetadataProperty property2 &&
                    (property1.Columns.Count > 1 || property2.Columns.Count > 1));
        }
        private bool IsColumnVariable(SyntaxNode node1, SyntaxNode node2)
        {
            return (node1 is ColumnReference column &&
                node2 is VariableReference &&
                column.Binding is MetadataProperty property &&
                property.Columns.Count > 1);
        }
        private bool IsVariableColumn(SyntaxNode node1, SyntaxNode node2)
        {
            return (node1 is VariableReference &&
                node2 is ColumnReference column &&
                column.Binding is MetadataProperty property &&
                property.Columns.Count > 1);
        }
        private bool IsColumnScalar(SyntaxNode node1, SyntaxNode node2)
        {
            return (node1 is ColumnReference column &&
                node2 is ScalarExpression &&
                column.Binding is MetadataProperty property &&
                property.Columns.Count > 1);
        }
        private bool IsScalarColumn(SyntaxNode node1, SyntaxNode node2)
        {
            return (node1 is ScalarExpression &&
                node2 is ColumnReference column &&
                column.Binding is MetadataProperty property &&
                property.Columns.Count > 1);
        }

        private GroupOperator Transform(ComparisonOperator comparison, SyntaxNode node1, SyntaxNode node2)
        {
            int tag = (int)ColumnPurpose.Tag;
            int tref = (int)ColumnPurpose.TypeCode;
            int rref = (int)ColumnPurpose.Identity;

            object[] union1 = CreateUnion(node1);
            object[] union2 = CreateUnion(node2);

            if (union1[tag] is int tag1 && union2[tag] is int tag2)
            {
                if (tag1 == tag2)
                {
                    union1[tag] = null!;
                    union2[tag] = null!;
                }
                else
                {
                    ThrowUnableToCompareException(node1, node2);
                }
            }

            if (union1[tref] is int tref1 && union2[tref] is int tref2)
            {
                if (tref1 == tref2)
                {
                    union1[tref] = null!;
                    union2[tref] = null!;
                }
                else
                {
                    ThrowUnableToCompareException(node1, node2);
                }
            }

            GroupOperator group = new();

            for (int type = tag; type <= rref; type++)
            {
                if (union1[type] != null && union2[type] != null)
                {
                    if (group.Expression == null)
                    {
                        group.Expression = CreateComparisonOperator(comparison.Token, node1, node2, type, union1, union2);
                    }
                    else
                    {
                        group.Expression = new BinaryOperator()
                        {
                            Token = TokenType.AND,
                            Expression1 = group.Expression,
                            Expression2 = CreateComparisonOperator(comparison.Token, node1, node2, type, union1, union2)
                        };
                    }
                }
            }

            if (group.Expression == null)
            {
                return null!; // no compatible types are found to compare
            }

            return group;
        }

        private object[] CreateUnion(SyntaxNode node)
        {
            if (node is VariableReference variable)
            {
                return ConvertVariableToUnion(variable);
            }

            if (node is ScalarExpression scalar)
            {
                return ConvertScalarToUnion(scalar);
            }
            
            if (node is TypeIdentifier type)
            {
                return ConvertTypeToUnion(type);
            }

            if (node is ColumnReference column &&
                column.Binding is MetadataProperty property &&
                property.Columns.Count > 0)
            {
                if (property.Columns.Count == 1)
                {
                    return ConvertSingleToUnion(property);
                }
                else
                {
                    return CreateUnion(property);
                }
            }
            
            return null;
        }
        private object[] CreateUnion(MetadataProperty property)
        {
            if (property.Columns.Count == 1)
            {
                return ConvertSingleToUnion(property);
            }

            object[] union = new object[((int)ColumnPurpose.Identity) + 1];

            MetadataColumn field;

            for (int i = 0; i < property.Columns.Count; i++)
            {
                field = property.Columns[i];

                union[(int)field.Purpose] = field;
            }

            if (union[(int)ColumnPurpose.Identity] != null &&
                union[(int)ColumnPurpose.Tag] == null)
            {
                // only reference type - tag is constant
                union[(int)ColumnPurpose.Tag] = (int)ColumnPurpose.Identity; // 0x08
            }

            if (union[(int)ColumnPurpose.Identity] != null &&
                union[(int)ColumnPurpose.TypeCode] == null)
            {
                // single reference type - type code is constant
                union[(int)ColumnPurpose.TypeCode] = property.PropertyType.TypeCode;
            }

            return union;
        }
        private object[] ConvertSingleToUnion(MetadataProperty property)
        {
            object[] union = new object[((int)ColumnPurpose.Identity) + 1];
            
            int value = 0;
            int tag = (int)ColumnPurpose.Tag;
            DataTypeSet type = property.PropertyType;
            MetadataColumn field = property.Columns[0];

            if (type.IsUuid || type.IsBinary || type.IsValueStorage)
            {
                value = (int)ColumnPurpose.Binary;
            }
            else if (type.CanBeBoolean)
            {
                value = (int)ColumnPurpose.Boolean;
            }
            else if (type.CanBeNumeric)
            {
                value = (int)ColumnPurpose.Numeric;
            }
            else if (type.CanBeDateTime)
            {
                value = (int)ColumnPurpose.DateTime;
            }
            else if (type.CanBeString)
            {
                value = (int)ColumnPurpose.String;
            }
            else if (type.CanBeReference)
            {
                value = (int)ColumnPurpose.Identity;
                union[(int)ColumnPurpose.TypeCode] = type.TypeCode;
            }

            union[tag] = value;
            union[value] = field;

            return union;
        }
        private object[] ConvertScalarToUnion(ScalarExpression scalar)
        {
            object[] union = new object[((int)ColumnPurpose.Identity) + 1];

            int tag = (int)ColumnPurpose.Tag;
            int value = tag; // Undefined

            if (scalar.Token == TokenType.Boolean)
            {
                value = (int)ColumnPurpose.Boolean;
            }
            else if (scalar.Token == TokenType.Number)
            {
                value = (int)ColumnPurpose.Numeric;
            }
            else if (scalar.Token == TokenType.DateTime)
            {
                value = (int)ColumnPurpose.DateTime;
            }
            else if (scalar.Token == TokenType.String)
            {
                value = (int)ColumnPurpose.String;
            }
            else if (scalar.Token == TokenType.Uuid || scalar.Token == TokenType.Binary)
            {
                value = (int)ColumnPurpose.Binary;
            }
            else if (scalar.Token == TokenType.Entity)
            {
                value = (int)ColumnPurpose.Identity;
                union[(int)ColumnPurpose.TypeCode] = Entity.Parse(scalar.Literal).TypeCode;
            }

            union[tag] = value;
            union[value] = scalar.Literal;

            return union;
        }
        private object[] ConvertVariableToUnion(VariableReference variable)
        {
            object[] union = new object[((int)ColumnPurpose.Identity) + 1];

            int tag = (int)ColumnPurpose.Tag;
            int value = tag;

            if (variable.Binding is Type type)
            {
                if (type == typeof(bool))
                {
                    value = (int)ColumnPurpose.Boolean;
                }
                else if (type == typeof(decimal))
                {
                    value = (int)ColumnPurpose.Numeric;
                }
                else if (type == typeof(DateTime))
                {
                    value = (int)ColumnPurpose.DateTime;
                }
                else if (type == typeof(string))
                {
                    value = (int)ColumnPurpose.String;
                }
                else if (type == typeof(Guid) || type == typeof(byte[]))
                {
                    value = (int)ColumnPurpose.Binary;
                }
            }
            else if (variable.Binding is Entity reference)
            {
                value = (int)ColumnPurpose.Identity;
                union[(int)ColumnPurpose.TypeCode] = reference.TypeCode;
            }

            union[tag] = value;
            union[value] = variable.Binding;

            return union;
        }
        private object[] ConvertTypeToUnion(TypeIdentifier identifier)
        {
            object[] union = new object[((int)ColumnPurpose.Identity) + 1];

            int tag = (int)ColumnPurpose.Tag; // адрес значения поля _TYPE
            int code = (int)ColumnPurpose.TypeCode; // адрес значения поля _TRef

            if (identifier.Binding is Type type)
            {
                if (type == typeof(Union)) // undefined
                {
                    union[tag] = (int)ColumnPurpose.Tag;
                }
                else if (type == typeof(bool)) // boolean
                {
                    union[tag] = (int)ColumnPurpose.Boolean;
                }
                else if (type == typeof(decimal)) // number
                {
                    union[tag] = (int)ColumnPurpose.Numeric;
                }
                else if (type == typeof(DateTime)) // datetime
                {
                    union[tag] = (int)ColumnPurpose.DateTime;
                }
                else if (type == typeof(string)) // string
                {
                    union[tag] = (int)ColumnPurpose.String;
                }
                else
                {
                    throw new FormatException($"Unknown type identifier: {identifier.Identifier}");
                }
            }
            else if (identifier.Binding is Entity entity)
            {
                union[tag] = (int)ColumnPurpose.Identity; // 0x08 - значение поля _TYPE
                union[code] = entity.TypeCode; // integer - значение поля _TRef
            }
            else
            {
                throw new FormatException($"Unknown type identifier: {identifier.Identifier}");
            }
            
            return union;
        }

        private ComparisonOperator CreateComparisonOperator(TokenType type, SyntaxNode column1, SyntaxNode column2, int tag, object[] union1, object[] union2)
        {
            ComparisonOperator comparison = new()
            {
                Token = type
            };

            if (union1[tag] is int value1)
            {
                comparison.Expression1 = new ScalarExpression()
                {
                    Token = TokenType.Number,
                    Literal = DbUtilities.GetBinaryLiteral((ColumnPurpose)tag, value1)
                };
            }
            else
            {
                comparison.Expression1 = CreateSyntaxNode(in column1, union1[tag]);
            }

            if (union2[tag] is int value2)
            {
                comparison.Expression2 = new ScalarExpression()
                {
                    Token = TokenType.Number,
                    Literal = DbUtilities.GetBinaryLiteral((ColumnPurpose)tag, value2)
                };
            }
            else
            {
                comparison.Expression2 = CreateSyntaxNode(in column2, union2[tag]);
            }

            return comparison;
        }
        private SyntaxNode CreateSyntaxNode(in SyntaxNode node, object binding)
        {
            if (node is ColumnReference property)
            {
                ColumnReference column = new()
                {
                    Binding = binding, // database column
                    Identifier = property.Identifier
                };

                ScriptHelper.GetColumnIdentifiers(property.Identifier, out string tableAlias, out string _);

                if (column.Binding is MetadataColumn source)
                {
                    ColumnMap map = new()
                    {
                        Type = UnionType.GetPurposeUnionTag(source.Purpose),
                        Name = string.IsNullOrEmpty(tableAlias) ? source.Name : $"{tableAlias}.{source.Name}"
                    };
                    column.Mapping = new List<ColumnMap>() { map };
                }

                return column;
            }
            else if (node is VariableReference variable)
            {
                return variable;
            }
            else if (node is ScalarExpression scalar)
            {
                return scalar;
            }

            return null; // TODO: throw error - unable to compare types
        }

        private void ThrowUnableToCompareException(SyntaxNode node1, SyntaxNode node2)
        {
            throw new InvalidCastException($"Unable to compare [{node1}] and [{node2}]");
        }

        #endregion

        #region "<column> IS <type>"

        private MetadataColumn GetColumnToCompareToNull(MetadataProperty property)
        {
            for (int i = 0; i < property.Columns.Count; i++)
            {
                if (property.Columns[i].Purpose == ColumnPurpose.Tag)
                {
                    return property.Columns[i];
                }
            }

            for (int i = 0; i < property.Columns.Count; i++)
            {
                if (property.Columns[i].Purpose == ColumnPurpose.TypeCode)
                {
                    return property.Columns[i];
                }
            }

            return property.Columns[0];
        }
        private SyntaxNode TransformColumnIsType(in ComparisonOperator comparison)
        {
            TokenType _operator;
            SyntaxNode leftOperand = comparison.Expression1;
            SyntaxNode rigthOperand = comparison.Expression2;

            if (rigthOperand is UnaryOperator unary)
            {
                _operator = TokenType.NotEquals;
                rigthOperand = unary.Expression;
            }
            else
            {
                _operator = TokenType.Equals;
            }

            if (IsSimpleIsNullOperator(leftOperand, rigthOperand))
            {
                return null; // no transformation is needed
            }

            if (!IsUnionColumn(leftOperand, out ColumnReference column))
            {
                throw new FormatException($"IS operator: left operand must be the union type column.");
            }

            if (IsNullScalar(rigthOperand)) // _Fld_TYPE IS [NOT] NULL
            {
                if (column.Binding is MetadataProperty property)
                {
                    column.Binding = GetColumnToCompareToNull(property);
                }
                return null;
            }

            if (!IsTypeIdentifier(rigthOperand, out TypeIdentifier identifier))
            {
                throw new FormatException($"IS operator: right operand is not valid type identifier.");
            }
            
            comparison.Token = _operator;

            return Transform(comparison, column, identifier);
        }

        #endregion
    }
}