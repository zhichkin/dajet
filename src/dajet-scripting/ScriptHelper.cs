using DaJet.Data;

namespace DaJet.Scripting
{
    public static class ScriptHelper
    {
        private static Dictionary<string, TokenType> _keywords_en = new()
        {
            { "WITH", TokenType.WITH },
            { "SELECT", TokenType.SELECT },
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
            { "ON", TokenType.ON },
            { "DECLARE", TokenType.DECLARE },
            { "NOLOCK", TokenType.NOLOCK },
            { "ROWLOCK", TokenType.ROWLOCK },
            { "READPAST", TokenType.READPAST },
            { "UPDLOCK", TokenType.UPDLOCK },
            { "SERIALIZABLE", TokenType.SERIALIZABLE },
            { "READCOMMITTEDLOCK", TokenType.READCOMMITTEDLOCK },
            { "INSERT", TokenType.INSERT },
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
            { "END", TokenType.END },
            { "UNION", TokenType.UNION },
            { "ALL", TokenType.ALL }
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
            { "ВСЕ", TokenType.ALL }
        };
        private static Dictionary<string, Type> _datatype_en = new()
        {
            { "uuid", typeof(Guid) },
            { "boolean", typeof(bool) },
            { "number", typeof(decimal) },
            { "datetime", typeof(DateTime) },
            { "string", typeof(string) },
            { "binary", typeof(byte[]) },
            { "entity", typeof(Entity) }
        };
        private static Dictionary<string, Type> _datatype_ru = new()
        {
            { "УникальныйИдентификатор", typeof(Guid) },
            { "Булево", typeof(bool) },
            { "Число", typeof(decimal) },
            { "ДатаВремя", typeof(DateTime) },
            { "Строка", typeof(string) },
            { "ДвоичныеДанные", typeof(byte[]) },
            { "Ссылка", typeof(Entity) }
        };
        private static Dictionary<Type, TokenType> _datatype_token = new()
        {
            { typeof(Guid), TokenType.Uuid },
            { typeof(bool), TokenType.Boolean },
            { typeof(int), TokenType.Number },
            { typeof(decimal), TokenType.Number },
            { typeof(DateTime), TokenType.DateTime },
            { typeof(string), TokenType.String },
            { typeof(byte[]), TokenType.Binary },
            { typeof(Entity), TokenType.Entity }
        };
        private static Dictionary<Type, string> _datatype_literal = new()
        {
            { typeof(Guid), "uuid" },
            { typeof(bool), "boolean" },
            { typeof(int), "number" },
            { typeof(decimal), "number" },
            { typeof(DateTime), "datetime" },
            { typeof(string), "string" },
            { typeof(byte[]), "binary" },
            { typeof(Entity), "entity" }
        };
        private static Dictionary<string, TokenType> _function_en = new()
        {
            { "SUM", TokenType.SUM },
            { "MAX", TokenType.MAX },
            { "MIN", TokenType.MIN },
            { "AVG", TokenType.AVG },
            { "COUNT", TokenType.COUNT }
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
        internal static bool IsDataType(string identifier, out Type type)
        {
            if (_datatype_ru.TryGetValue(identifier, out type!))
            {
                return true;
            }
            return _datatype_en.TryGetValue(identifier, out type!);
        }
        public static TokenType GetDataTypeToken(Type type)
        {
            if (_datatype_token.TryGetValue(type, out TokenType token))
            {
                return token;
            }
            throw new NotSupportedException($"Unsupported data type token {type}");
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
            return character == '_'
                || character == '.' // multipart identifier
                || (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z')
                || (character >= 'А' && character <= 'Я')
                || (character >= 'а' && character <= 'я');
        }
        internal static bool IsNumeric(char character)
        {
            return (character >= '0' && character <= '9');
        }
        internal static bool IsAlphaNumeric(char character)
        {
            return IsAlpha(character) || IsNumeric(character);
        }

        internal static bool IsFunction(string identifier, out TokenType token)
        {
            if (_function_ru.TryGetValue(identifier, out token))
            {
                return true;
            }
            return _function_en.TryGetValue(identifier.ToUpperInvariant(), out token);
        }

        internal static void GetColumnNames(string identifier, out string tableAlias, out string columnName)
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
        internal static string GetColumnName(string identifier)
        {
            string[] names = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);

            //string tableAlias = string.Empty;
            string columnName = string.Empty;

            if (names.Length > 1)
            {
                //tableAlias = names[0];
                columnName = names[1];
            }
            else
            {
                columnName = names[0];
            }

            return columnName;
        }

        internal static string GetComparisonLiteral(TokenType token)
        {
            if (token == TokenType.Equals) { return "="; }
            else if (token == TokenType.NotEquals) { return "<>"; }
            else if (token == TokenType.Less) { return "<"; }
            else if (token == TokenType.LessOrEquals) { return "<="; }
            else if (token == TokenType.Greater) { return ">"; }
            else if (token == TokenType.GreaterOrEquals) { return ">="; }

            return token.ToString();
        }
        internal static string GetColumnPurposePostfix(ColumnPurpose purpose)
        {
            if (purpose == ColumnPurpose.Default) { return string.Empty; }
            else if (purpose == ColumnPurpose.Tag)      { return "_TYPE"; }
            else if (purpose == ColumnPurpose.Boolean)  { return "_L"; }
            else if (purpose == ColumnPurpose.Numeric)  { return "_N"; }
            else if (purpose == ColumnPurpose.DateTime) { return "_T"; }
            else if (purpose == ColumnPurpose.String)   { return "_S"; }
            else if (purpose == ColumnPurpose.Binary)   { return "_B"; }
            else if (purpose == ColumnPurpose.TypeCode) { return "_TRef"; }
            else if (purpose == ColumnPurpose.Identity) { return "_RRef"; }

            return string.Empty;
        }
    }
}