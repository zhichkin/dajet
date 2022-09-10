using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class ComparisonOperatorTransformer
    {
        public SyntaxNode Transform(in ComparisonOperator comparison)
        {
            // TODO:
            // 1. Ссылка = @Ссылка
            // 2. Ссылка1 = Ссылка2 (составные типы)
            // 3. Ссылка ССЫЛКА Справочник.Номенклатура
            // 4. ТИПЗНАЧЕНИЯ(Ссылка) = ТИП(Справочник.Номенклатура)

            if (comparison.Expression1 is Identifier identifier1 &&
                comparison.Expression2 is Identifier identifier2)
            {
                if (IsColumnColumn(identifier1, identifier2))
                {
                    return Transform(comparison, identifier1, identifier2);
                    //return TransformColumnColumn(comparison, identifier1, identifier2);
                }
                else if (IsColumnVariable(identifier1, identifier2))
                {
                    return Transform(comparison, identifier1, identifier2);
                    //return TransformColumnVariable(comparison, identifier1, identifier2);
                }
                else if (IsVariableColumn(identifier1, identifier2))
                {
                    return Transform(comparison, identifier1, identifier2);
                    //return TransformColumnVariable(comparison, identifier2, identifier1);
                }
            }

            return null!; // no transformation is needed
        }

        private bool IsColumnColumn(Identifier identifier1, Identifier identifier2)
        {
            return (identifier1.Token == TokenType.Column &&
                    identifier2.Token == TokenType.Column &&
                    identifier1.Tag is MetadataProperty property1 &&
                    identifier2.Tag is MetadataProperty property2 &&
                    (property1.Columns.Count > 1 || property2.Columns.Count > 1));
        }
        private bool IsColumnVariable(Identifier identifier1, Identifier identifier2)
        {
            return (identifier1.Token == TokenType.Column &&
                    identifier1.Tag is MetadataProperty property &&
                    property.Columns.Count > 1 &&
                    identifier2.Token == TokenType.Variable);
        }
        private bool IsVariableColumn(Identifier identifier1, Identifier identifier2)
        {
            return (identifier1.Token == TokenType.Variable &&
                    identifier2.Token == TokenType.Column &&
                    identifier2.Tag is MetadataProperty property &&
                    property.Columns.Count > 1);
        }

        private BooleanGroupExpression Transform(ComparisonOperator comparison, Identifier identifier1, Identifier identifier2)
        {
            int tag = (int)ColumnPurpose.Tag;
            int tref = (int)ColumnPurpose.TypeCode;
            int rref = (int)ColumnPurpose.Identity;

            object[] union1 = CreateUnion(identifier1);
            object[] union2 = CreateUnion(identifier2);

            if (union1[tag] is int tag1 && union2[tag] is int tag2)
            {
                if (tag1 == tag2)
                {
                    union1[tag] = null!;
                    union2[tag] = null!;
                }
                else
                {
                    ThrowUnableToCompareException(identifier1, identifier2);
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
                    ThrowUnableToCompareException(identifier1, identifier2);
                }
            }

            BooleanGroupExpression group = new();

            for (int type = tag; type <= rref; type++)
            {
                if (union1[type] != null && union2[type] != null)
                {
                    if (group.Expression == null)
                    {
                        group.Expression = CreateComparisonOperator(comparison.Token, identifier1, identifier2, type, union1, union2);
                    }
                    else
                    {
                        group.Expression = new BooleanBinaryOperator()
                        {
                            Token = TokenType.AND,
                            Expression1 = group.Expression,
                            Expression2 = CreateComparisonOperator(comparison.Token, identifier1, identifier2, type, union1, union2)
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

        private object[] CreateUnion(Identifier identifier)
        {
            if (identifier.Token == TokenType.Variable)
            {
                return ConvertVariableToUnion(identifier);
            }

            if (identifier.Token == TokenType.Column &&
                identifier.Tag is MetadataProperty property &&
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

            return null!;
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
        private object[] ConvertVariableToUnion(Identifier variable)
        {
            object[] union = new object[((int)ColumnPurpose.Identity) + 1];

            int tag = (int)ColumnPurpose.Tag;
            int value = tag;

            if (variable.Tag is Type type)
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
            else if (variable.Tag is EntityRef reference)
            {
                value = (int)ColumnPurpose.Identity;
                union[(int)ColumnPurpose.TypeCode] = reference.TypeCode;
            }

            union[tag] = value;
            union[value] = variable.Tag;

            return union;
        }

        private ComparisonOperator CreateComparisonOperator(TokenType type, Identifier column1, Identifier column2, int tag, object[] union1, object[] union2)
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
                comparison.Expression1 = new Identifier()
                {
                    Tag = union1[tag],
                    Token = column1.Token,
                    Value = column1.Value,
                    Alias = column1.Alias
                };
                
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
                comparison.Expression2 = new Identifier()
                {
                    Tag = union2[tag],
                    Token = column2.Token,
                    Value = column2.Value,
                    Alias = column2.Alias
                };

            }

            return comparison;
        }

        private void ThrowUnableToCompareException(Identifier identifier1, Identifier identifier2)
        {
            string message = "Unable to compare "
                + "[" + identifier1.Token + ": " + identifier1.Value + "]"
                + " and "
                + "[" + identifier2.Token + ": " + identifier2.Value + "]";

            throw new InvalidCastException(message);
        }
    }
}