using DaJet.Data;
using DaJet.Scripting.Model;
using System.Globalization;

namespace DaJet.Scripting
{
    public static class ParserHelper
    {
        private static Dictionary<string, Type> _datatype = new()
        {
            { "boolean", typeof(bool) }, // L
            { "number", typeof(decimal) }, // N
            { "datetime", typeof(DateTime) }, // T
            { "string", typeof(string) }, // S
            { "binary", typeof(byte[]) }, // B
            { "uuid", typeof(Guid) }, // U
            { "entity", typeof(Entity) }, // #
            { "union", typeof(Union) },
            { "array", typeof(Array) },
            { "object", typeof(object) }
        };
        private static Dictionary<Type, string> _datatype_literal = new()
        {
            { typeof(bool), "boolean" },
            { typeof(int), "number" },
            { typeof(decimal), "number" },
            { typeof(DateTime), "datetime" },
            { typeof(string), "string" },
            { typeof(byte[]), "binary" },
            { typeof(Guid), "uuid" },
            { typeof(Entity), "entity" },
            { typeof(Union), "union" },
            { typeof(Array), "array" },
            { typeof(object), "object" },
            { typeof(DataObject), "object" },
            { typeof(List<DataObject>), "array" }
        };
        private static Dictionary<Type, TokenType> _datatype_token = new()
        {
            { typeof(bool), TokenType.Boolean },
            { typeof(int), TokenType.Number },
            { typeof(ulong), TokenType.Number },
            { typeof(decimal), TokenType.Number },
            { typeof(DateTime), TokenType.DateTime },
            { typeof(string), TokenType.String },
            { typeof(byte[]), TokenType.Binary },
            { typeof(Guid), TokenType.Uuid },
            { typeof(Entity), TokenType.Entity },
            { typeof(Union), TokenType.Union },
            { typeof(Array), TokenType.Array },
            { typeof(object), TokenType.Object }
        };
        private static Dictionary<TokenType, Type> _token_datatype = new()
        {
            { TokenType.Boolean, typeof(bool) },
            { TokenType.Number, typeof(decimal) },
            { TokenType.Integer, typeof(int) },
            { TokenType.Version, typeof(ulong) },
            { TokenType.DateTime, typeof(DateTime) },
            { TokenType.String, typeof(string) },
            { TokenType.Binary, typeof(byte[]) },
            { TokenType.Uuid, typeof(Guid) },
            { TokenType.Entity, typeof(Entity) },
            { TokenType.Union, typeof(Union) },
            { TokenType.Array, typeof(Array) },
            { TokenType.Object, typeof(object) }
        };

        private static Dictionary<string, TokenType> _keywords_en = new()
        {
            { "WITH", TokenType.WITH },
            { "SELECT", TokenType.SELECT },
            { "INTO", TokenType.INTO },
            { "DISTINCT", TokenType.DISTINCT },
            { "TOP", TokenType.TOP },
            { "FROM", TokenType.FROM },
            { "WHERE", TokenType.WHERE },
            { "ORDER", TokenType.ORDER },
            { "BY", TokenType.BY },
            { "ASC", TokenType.ASC },
            { "DESC", TokenType.DESC },
            { "AND", TokenType.AND },
            { "OR", TokenType.OR },
            { "AS", TokenType.AS },
            { "NOT", TokenType.NOT },
            { "JOIN", TokenType.JOIN },
            { "LEFT", TokenType.LEFT },
            { "RIGHT", TokenType.RIGHT },
            { "FULL", TokenType.FULL },
            { "INNER", TokenType.INNER },
            { "CROSS", TokenType.CROSS },
            { "OUTER", TokenType.OUTER },
            { "APPLY", TokenType.APPLY },
            { "ON", TokenType.ON },
            { "DECLARE", TokenType.DECLARE },
            { "NOLOCK", TokenType.NOLOCK },
            { "ROWLOCK", TokenType.ROWLOCK },
            { "READPAST", TokenType.READPAST },
            { "UPDLOCK", TokenType.UPDLOCK },
            { "SERIALIZABLE", TokenType.SERIALIZABLE },
            { "READCOMMITTEDLOCK", TokenType.READCOMMITTEDLOCK },
            { "INSERT", TokenType.INSERT },
            { "VALUES", TokenType.VALUES },
            { "UPDATE", TokenType.UPDATE },
            { "DELETE", TokenType.DELETE },
            { "OUTPUT", TokenType.OUTPUT },
            { "SET", TokenType.SET },
            { "ROW", TokenType.ROW },
            { "ROWS", TokenType.ROWS },
            { "ONLY", TokenType.ONLY },
            { "OFFSET", TokenType.OFFSET },
            { "FETCH", TokenType.FETCH },
            { "FIRST", TokenType.FIRST },
            { "NEXT", TokenType.NEXT },
            { "GROUP", TokenType.GROUP },
            { "HAVING", TokenType.HAVING },
            { "OVER", TokenType.OVER },
            { "PARTITION", TokenType.PARTITION },
            { "RANGE", TokenType.RANGE },
            { "BETWEEN", TokenType.BETWEEN },
            { "UNBOUNDED", TokenType.UNBOUNDED },
            { "PRECEDING", TokenType.PRECEDING },
            { "CURRENT", TokenType.CURRENT },
            { "FOLLOWING", TokenType.FOLLOWING },
            { "CASE", TokenType.CASE },
            { "WHEN", TokenType.WHEN },
            { "THEN", TokenType.THEN },
            { "ELSE", TokenType.ELSE },
            { "BEGIN", TokenType.BEGIN },
            { "END", TokenType.END },
            { "EXISTS", TokenType.EXISTS },
            { "UNION", TokenType.UNION },
            { "ALL", TokenType.ALL },
            { "ANY", TokenType.ANY },
            { "IN", TokenType.IN },
            { "LIKE", TokenType.LIKE },
            { "IS", TokenType.IS },
            { "NULL", TokenType.NULL },
            { "CREATE", TokenType.CREATE },
            { "TABLE", TokenType.TABLE },
            { "COMPUTED", TokenType.COMPUTED },
            { "VARIABLE", TokenType.VARIABLE },
            { "TEMPORARY", TokenType.TEMPORARY },
            { "UPSERT", TokenType.UPSERT },
            { "IGNORE", TokenType.IGNORE },
            { "TYPE", TokenType.TYPE },
            { "COLUMN", TokenType.COLUMN },
            { "OF", TokenType.OF },
            { "DROP", TokenType.DROP },
            { "CONSUME", TokenType.CONSUME },
            { "STRICT", TokenType.STRICT },
            { "RANDOM", TokenType.RANDOM },
            { "IMPORT", TokenType.IMPORT },
            { "SEQUENCE", TokenType.SEQUENCE },
            { "START", TokenType.START },
            { "INCREMENT", TokenType.INCREMENT },
            { "CACHE", TokenType.CACHE },
            { "USE", TokenType.USE },
            { "APPEND", TokenType.APPEND },
            { "FOR", TokenType.FOR },
            { "EACH", TokenType.EACH },
            { "MAXDOP", TokenType.MAXDOP },
            { "PRODUCE", TokenType.PRODUCE },
            { "STREAM", TokenType.STREAM },
            { "REQUEST", TokenType.REQUEST },
            { "REVOKE", TokenType.REVOKE },
            { "RECALCULATE", TokenType.RECALCULATE },
            { "IF", TokenType.IF },
            { "WHILE", TokenType.WHILE },
            { "PRINT", TokenType.PRINT },
            { "RETURN", TokenType.RETURN },
            { "BREAK", TokenType.BREAK },
            { "CONTINUE", TokenType.CONTINUE },
            { "EXECUTE", TokenType.EXECUTE },
            { "PROCESS", TokenType.PROCESS },
            { "TRY", TokenType.TRY },
            { "CATCH", TokenType.CATCH },
            { "FINALLY", TokenType.FINALLY },
            { "THROW", TokenType.THROW },
            { "SLEEP", TokenType.SLEEP },
            { "DEFAULT", TokenType.DEFAULT },
            { "TASK", TokenType.TASK },
            { "WORK", TokenType.WORK },
            { "SYNC", TokenType.SYNC },
            { "WAIT", TokenType.WAIT },
            { "TIMEOUT", TokenType.TIMEOUT },
        };
        private static Dictionary<string, TokenType> _keywords_ru = new()
        {
            { "ВЫБРАТЬ", TokenType.SELECT },
            { "РАЗЛИЧНЫЕ", TokenType.DISTINCT },
            { "ПЕРВЫЕ", TokenType.TOP },
            { "ИЗ", TokenType.FROM },
            { "ГДЕ", TokenType.WHERE },
            { "УПОРЯДОЧИТЬ", TokenType.ORDER },
            { "ВОЗР", TokenType.ASC },
            { "УБЫВ", TokenType.DESC },
            { "И", TokenType.AND },
            { "ИЛИ", TokenType.OR },
            { "КАК", TokenType.AS },
            { "НЕ", TokenType.NOT },
            { "СОЕДИНЕНИЕ", TokenType.JOIN },
            { "ЛЕВОЕ", TokenType.LEFT },
            { "ПРАВОЕ", TokenType.RIGHT },
            { "ПОЛНОЕ", TokenType.FULL },
            { "ВНУТРЕННЕЕ", TokenType.INNER },
            { "ПЕРЕКРЕСТНОЕ", TokenType.CROSS },
            { "ПО", TokenType.ON },
            { "ОБЪЯВИТЬ", TokenType.DECLARE },
            { "УДАЛИТЬ", TokenType.DELETE },
            { "ДОБАВИТЬ", TokenType.INSERT },
            { "ОБНОВИТЬ", TokenType.UPDATE },
            { "ВЫВЕСТИ", TokenType.OUTPUT },
            { "СГРУППИРОВАТЬ", TokenType.GROUP },
            { "ИМЕЮЩИЕ", TokenType.HAVING },
            { "ВЫБОР", TokenType.CASE },
            { "КОГДА", TokenType.WHEN },
            { "ТОГДА", TokenType.THEN },
            { "ИНАЧЕ", TokenType.ELSE },
            { "КОНЕЦ", TokenType.END },
            { "ОБЪЕДИНИТЬ", TokenType.UNION },
            { "ВСЕ", TokenType.ALL },
            { "ЕСТЬ", TokenType.IS }
        };
        
        private static Dictionary<string, TokenType> _function_en = new()
        {
            { "SUM", TokenType.SUM },
            { "MAX", TokenType.MAX },
            { "MIN", TokenType.MIN },
            { "AVG", TokenType.AVG },
            { "COUNT", TokenType.COUNT },
            { "ISNULL", TokenType.ISNULL },
            { "ROW_NUMBER", TokenType.ROW_NUMBER },
            { "SUBSTRING", TokenType.SUBSTRING },
            { "DATALENGTH", TokenType.DATALENGTH },
            { "NOW", TokenType.NOW },
            { "VECTOR", TokenType.VECTOR },
            { "STRING_AGG", TokenType.STRING_AGG },
            { "CHARLENGTH", TokenType.CHARLENGTH },
            { "CONCAT", TokenType.CONCAT },
            { "CONCAT_WS", TokenType.CONCAT_WS },
            { "REPLACE", TokenType.REPLACE },
            { "LOWER", TokenType.LOWER },
            { "UPPER", TokenType.UPPER },
            { "LTRIM", TokenType.LTRIM },
            { "RTRIM", TokenType.RTRIM },
            { "LAG", TokenType.LAG },
            { "LEAD", TokenType.LEAD },
            { "FIRST_VALUE", TokenType.FIRST_VALUE },
            { "LAST_VALUE", TokenType.LAST_VALUE },
            { "NEWUUID", TokenType.NEWUUID }
        };
        private static Dictionary<string, TokenType> _function_ru = new()
        {
            { "СУММА", TokenType.SUM },
            { "МАКСИМУМ", TokenType.MAX },
            { "МИНИМУМ", TokenType.MIN },
            { "СРЕДНЕЕ", TokenType.AVG },
            { "КОЛИЧЕСТВО", TokenType.COUNT }
        };
        
        internal static bool IsKeyword(string identifier, out TokenType token)
        {
            if (_keywords_ru.TryGetValue(identifier.ToUpperInvariant(), out token))
            {
                return true;
            }
            return _keywords_en.TryGetValue(identifier.ToUpperInvariant(), out token);
        }
        public static bool IsDataType(string identifier, out Type type)
        {
            return _datatype.TryGetValue(identifier.ToLowerInvariant(), out type);
        }
        public static TokenType GetDataTypeToken(Type type)
        {
            if (_datatype_token.TryGetValue(type, out TokenType token))
            {
                return token;
            }
            throw new NotSupportedException($"Unsupported data type token {type}");
        }
        public static Type GetTokenDataType(TokenType token)
        {
            if (_token_datatype.TryGetValue(token, out Type type))
            {
                return type;
            }
            throw new NotSupportedException($"Unsupported data type token {token}");
        }
        public static string GetDataTypeLiteral(Type type)
        {
            if (_datatype_literal.TryGetValue(type, out string literal))
            {
                return literal;
            }
            throw new NotSupportedException($"Unsupported data type literal {type}");
        }
        internal static bool IsNullLiteral(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            string test = identifier.ToLowerInvariant();

            return (test == "null");
        }
        internal static bool IsTrueLiteral(string literal)
        {
            if (string.IsNullOrWhiteSpace(literal))
            {
                return false;
            }
            return ("true" == literal.ToLowerInvariant());
        }
        internal static bool IsFalseLiteral(string literal)
        {
            if (string.IsNullOrWhiteSpace(literal))
            {
                return false;
            }
            return ("false" == literal.ToLowerInvariant());
        }
        internal static bool IsBooleanLiteral(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            string test = identifier.ToLowerInvariant();

            return (test == "true" || test == "false");
        }
        internal static bool IsAlpha(char character)
        {
            return character == '_' || character == '-'
                || character == '[' || character == ']'
                || character == '\'' || character == '\''
                || character == '=' || character == '@'
                || character == '.' // multipart identifier
                || (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z')
                || (character >= 'А' && character <= 'Я')
                || (character >= 'а' && character <= 'я')
                || (character == 'Ё' || character == 'ё');
        }
        internal static bool IsNumeric(char character)
        {
            return (character >= '0' && character <= '9');
        }
        internal static bool IsAlphaNumeric(char character)
        {
            return IsAlpha(character) || IsNumeric(character);
        }
        internal static bool IsHexAlpha(char character)
        {
            return (character >= 'A' && character <= 'F')
                || (character >= 'a' && character <= 'f');
        }
        internal static bool IsHexadecimal(char character)
        {
            return IsNumeric(character) || IsHexAlpha(character);
        }

        internal static bool IsFunction(string identifier, out TokenType token)
        {
            if (_function_ru.TryGetValue(identifier, out token))
            {
                return true;
            }
            return _function_en.TryGetValue(identifier.ToUpperInvariant(), out token);
        }

        internal static void GetColumnIdentifiers(string identifier, out string tableAlias, out string columnName)
        {
            string[] names = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);

            tableAlias = string.Empty;
            columnName = string.Empty;

            if (names.Length == 0)
            {
                return;
            }

            columnName = names[names.Length - 1];

            if (names.Length > 1)
            {
                tableAlias = names[0];
            }
        }
        
        internal static string GetComparisonLiteral(TokenType token)
        {
            if (token == TokenType.IS) { return "IS"; }
            else if (token == TokenType.IN) { return "IN"; }
            else if (token == TokenType.LIKE) { return "LIKE"; }
            else if (token == TokenType.BETWEEN) { return "BETWEEN"; }
            else if (token == TokenType.Equals) { return "="; }
            else if (token == TokenType.NotEquals) { return "<>"; }
            else if (token == TokenType.Less) { return "<"; }
            else if (token == TokenType.LessOrEquals) { return "<="; }
            else if (token == TokenType.Greater) { return ">"; }
            else if (token == TokenType.GreaterOrEquals) { return ">="; }
            
            return token.ToString();
        }

        internal static string GetUuidHexLiteral(Guid uuid)
        {
            string value = uuid.ToString("N");

            return string.Concat(
                value.AsSpan(16, 16),
                value.AsSpan(12, 4),
                value.AsSpan(8, 4),
                value.AsSpan(0, 8));

            // SqlServer return $"0x{value}";

            // PostgreSql return $"CAST(E'\\\\x{value}' AS bytea)";
        }

        public static ScalarExpression CreateDefaultScalar(in DeclareStatement declare)
        {
            if (!IsDataType(declare.Type.Identifier, out Type type))
            {
                return new ScalarExpression()
                {
                    Token = TokenType.Uuid,
                    Literal = "00000000-0000-0000-0000-000000000000"
                };
            }

            string literal = string.Empty;
            TokenType token = TokenType.String;

            if (type == typeof(bool))
            {
                literal = "false";
                token = TokenType.Boolean;
            }
            else if (type == typeof(int))
            {
                literal = "0";
                token = TokenType.Integer;
            }
            else if (type == typeof(decimal))
            {
                literal = "0.00";
                token = TokenType.Number;
            }
            else if (type == typeof(DateTime))
            {
                literal = "0001-01-01T00:00:00";
                token = TokenType.DateTime;
            }
            else if (type == typeof(string))
            {
                literal = string.Empty;
                token = TokenType.String;
            }
            else if (type == typeof(byte[]))
            {
                literal = "0x00";
                token = TokenType.Binary;
            }
            else if (type == typeof(Guid))
            {
                literal = "00000000-0000-0000-0000-000000000000";
                token = TokenType.Uuid;
            }
            else if (type == typeof(Entity))
            {
                literal = "{0:00000000-0000-0000-0000-000000000000}";
                token = TokenType.Entity;
            }

            return new ScalarExpression() { Token = token, Literal = literal };
        }
        public static object GetScalarValue(in ScalarExpression scalar)
        {
            object value = null;
            string literal = scalar.Literal;
            TokenType token = scalar.Token;

            if (token == TokenType.NULL)
            {
                return null;
            }
            else if (token == TokenType.Boolean)
            {
                if (literal.ToLowerInvariant() == "true")
                {
                    value = true;
                }
                else if (literal.ToLowerInvariant() == "false")
                {
                    value = false;
                }
            }
            else if (token == TokenType.Number)
            {
                value = decimal.Parse(literal, CultureInfo.InvariantCulture);
            }
            else if (token == TokenType.Integer)
            {
                value = int.Parse(literal, CultureInfo.InvariantCulture);
            }
            else if (token == TokenType.DateTime)
            {
                value = DateTime.Parse(literal);
            }
            else if (token == TokenType.String)
            {
                value = literal;
            }
            else if (token == TokenType.Uuid)
            {
                value = new Guid(literal);
            }
            else if (token == TokenType.Binary)
            {
                value = DbUtilities.StringToByteArray(literal[2..]); // remove leading 0x
            }
            else if (token == TokenType.Entity)
            {
                // Metadata object reference parameter:
                // DECLARE @product entity = {50:9a1984dc-3084-11ed-9cd7-408d5c93cc8e};
                value = Entity.Parse(scalar.Literal);
            }

            return value;
        }

        public static List<string> GetAccessMembers(in string expression)
        {
            List<string> members = new();

            int position = 0;
            bool ignore_dot = false;

            for (int i = 0; i < expression.Length; i++)
            {
                if (expression[i] == '.' && !ignore_dot)
                {
                    if (position < i)
                    {
                        members.Add(expression[position..i]);
                    }

                    position = i + 1;
                }
                else if (expression[i] == '[')
                {
                    ignore_dot = true; //TODO: parse selector recursively

                    members.Add(expression[position..i]);

                    position = i;
                }
                else if (expression[i] == ']')
                {
                    members.Add(expression[position..(i + 1)]);

                    position = i + 1;

                    ignore_dot = false; //TODO: parse selector recursively
                }
            }

            members.Add(expression[position..]);

            return members;
        }
    }
}