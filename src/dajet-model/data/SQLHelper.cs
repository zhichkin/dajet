using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DaJet.Data
{
    public sealed class SqlFieldInfo
    {
        public SqlFieldInfo() { }
        public int ORDINAL_POSITION;
        public string COLUMN_NAME;
        public string DATA_TYPE;
        public int CHARACTER_MAXIMUM_LENGTH;
        public byte NUMERIC_PRECISION;
        public int NUMERIC_SCALE;
        public bool IS_NULLABLE;
    }
    public sealed class IndexInfo
    {
        public IndexInfo(string name, bool unique, bool clustered, bool primaryKey)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsUnique = unique;
            IsClustered = clustered;  //  1 - CLUSTERED, 2 - NONCLUSTERED
            IsPrimaryKey = primaryKey;
        }
        public string Name { get; private set; }
        public bool IsUnique { get; private set; }
        public bool IsClustered { get; private set; }
        public bool IsPrimaryKey { get; private set; }
        public List<IndexColumnInfo> Columns { get; } = new List<IndexColumnInfo>();
        public List<IndexColumnInfo> Includes { get; } = new List<IndexColumnInfo>();
        public override string ToString() { return Name; }
    }
    public sealed class IndexColumnInfo
    {
        public IndexColumnInfo(string name, string type, byte ordinal, bool included, bool nullable, bool descending)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TypeName = type;
            KeyOrdinal = ordinal;
            IsIncluded = included;  //  1 - CLUSTERED, 2 - NONCLUSTERED
            IsNullable = nullable;
            IsDescending = descending;
        }
        public string Name { get; private set; }
        public string TypeName { get; private set; }
        public byte KeyOrdinal { get; private set; }
        public bool IsIncluded { get; private set; }
        public bool IsNullable { get; private set; }
        public bool IsDescending { get; private set; } // 0 - ASC, 1 - DESC
        public override string ToString() { return Name; }
    }
    public sealed class ClusteredIndexInfo
    {
        public ClusteredIndexInfo() { }
        public string NAME;
        public bool IS_UNIQUE;
        public bool IS_PRIMARY_KEY;
        public List<ClusteredIndexColumnInfo> COLUMNS = new List<ClusteredIndexColumnInfo>();
        public bool HasNullableColumns
        {
            get
            {
                bool result = false;
                foreach (ClusteredIndexColumnInfo item in COLUMNS)
                {
                    if (item.IS_NULLABLE)
                    {
                        return true;
                    }
                }
                return result;
            }
        }
        public ClusteredIndexColumnInfo GetColumnByName(string name)
        {
            ClusteredIndexColumnInfo info = null;
            for (int i = 0; i < COLUMNS.Count; i++)
            {
                if (COLUMNS[i].NAME == name) return COLUMNS[i];
            }
            return info;
        }
    }
    public sealed class ClusteredIndexColumnInfo
    {
        public ClusteredIndexColumnInfo() { }
        public byte KEY_ORDINAL;
        public string NAME;
        public bool IS_NULLABLE;
        public bool IS_DESCENDING_KEY; // 0 - ASC, 1 - DESC
    }
    public static class SQLHelper
    {
        public static List<SqlFieldInfo> GetSqlFields(string connectionString, string tableName)
        {
            StringBuilder sb = new();
            sb.AppendLine(@"SELECT");
            sb.AppendLine(@"    ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE,");
            sb.AppendLine(@"    ISNULL(CHARACTER_MAXIMUM_LENGTH, 0) AS CHARACTER_MAXIMUM_LENGTH,");
            sb.AppendLine(@"    ISNULL(NUMERIC_PRECISION, 0) AS NUMERIC_PRECISION,");
            sb.AppendLine(@"    ISNULL(NUMERIC_SCALE, 0) AS NUMERIC_SCALE,");
            sb.AppendLine(@"    CASE WHEN IS_NULLABLE = 'NO' THEN CAST(0x00 AS bit) ELSE CAST(0x01 AS bit) END AS IS_NULLABLE");
            sb.AppendLine(@"FROM");
            sb.AppendLine(@"    INFORMATION_SCHEMA.COLUMNS");
            sb.AppendLine(@"WHERE");
            sb.AppendLine(@"    TABLE_NAME = N'{0}'");
            sb.AppendLine(@"ORDER BY");
            sb.AppendLine(@"    ORDINAL_POSITION ASC;");

            string sql = string.Format(sb.ToString(), tableName);

            List<SqlFieldInfo> list = new List<SqlFieldInfo>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SqlFieldInfo item = new SqlFieldInfo()
                            {
                                ORDINAL_POSITION = reader.GetInt32(0),
                                COLUMN_NAME = reader.GetString(1),
                                DATA_TYPE = reader.GetString(2),
                                CHARACTER_MAXIMUM_LENGTH = reader.GetInt32(3),
                                NUMERIC_PRECISION = reader.GetByte(4),
                                NUMERIC_SCALE = reader.GetInt32(5),
                                IS_NULLABLE = reader.GetBoolean(6)
                            };
                            list.Add(item);
                        }
                    }
                }
            }
            return list;
        }
        public static ClusteredIndexInfo GetClusteredIndexInfo(string connectionString, string tableName)
        {
            ClusteredIndexInfo info = null;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"SELECT");
            sb.AppendLine(@"    i.name,");
            sb.AppendLine(@"    i.is_unique,");
            sb.AppendLine(@"    i.is_primary_key,");
            sb.AppendLine(@"    c.key_ordinal,");
            sb.AppendLine(@"    c.is_descending_key,");
            sb.AppendLine(@"    f.name,");
            sb.AppendLine(@"    f.is_nullable");
            sb.AppendLine(@"FROM sys.indexes AS i");
            sb.AppendLine(@"INNER JOIN sys.tables AS t ON t.object_id = i.object_id");
            sb.AppendLine(@"INNER JOIN sys.index_columns AS c ON c.object_id = t.object_id AND c.index_id = i.index_id");
            sb.AppendLine(@"INNER JOIN sys.columns AS f ON f.object_id = t.object_id AND f.column_id = c.column_id");
            sb.AppendLine(@"WHERE");
            sb.AppendLine(@"    t.object_id = OBJECT_ID(@table) AND i.type = 1 -- CLUSTERED");
            sb.AppendLine(@"ORDER BY");
            sb.AppendLine(@"c.key_ordinal ASC;");
            string sql = sb.ToString();

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                connection.Open();

                command.Parameters.AddWithValue("table", tableName);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        info = new ClusteredIndexInfo()
                        {
                            NAME = reader.GetString(0),
                            IS_UNIQUE = reader.GetBoolean(1),
                            IS_PRIMARY_KEY = reader.GetBoolean(2)
                        };
                        info.COLUMNS.Add(new ClusteredIndexColumnInfo()
                        {
                            KEY_ORDINAL = reader.GetByte(3),
                            IS_DESCENDING_KEY = reader.GetBoolean(4),
                            NAME = reader.GetString(5),
                            IS_NULLABLE = reader.GetBoolean(6)
                        });
                        while (reader.Read())
                        {
                            info.COLUMNS.Add(new ClusteredIndexColumnInfo()
                            {
                                KEY_ORDINAL = reader.GetByte(3),
                                IS_DESCENDING_KEY = reader.GetBoolean(4),
                                NAME = reader.GetString(5),
                                IS_NULLABLE = reader.GetBoolean(6)
                            });
                        }
                    }
                }
            }
            return info;
        }

        private static string GetSelectIndexesScript()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(@"SELECT");
            sb.AppendLine(@"  i.name               AS [IndexName],");
            sb.AppendLine(@"  i.index_id           AS [IndexId],");
            sb.AppendLine(@"  c.key_ordinal        AS [KeyOrdinal],");
            sb.AppendLine(@"  i.type               AS [IsClustered],");
            sb.AppendLine(@"  i.is_unique          AS [IsUnique],");
            sb.AppendLine(@"  i.is_primary_key     AS [IsPrimaryKey],");
            sb.AppendLine(@"  c.is_descending_key  AS [IsDescending],");
            sb.AppendLine(@"  c.is_included_column AS [IsIncluded],");
            sb.AppendLine(@"  f.name               AS [ColumnName],");
            sb.AppendLine(@"  t.name               AS [TypeName],");
            sb.AppendLine(@"  f.is_nullable        AS [IsNullable]");
            sb.AppendLine(@"FROM  sys.indexes AS i");
            sb.AppendLine(@"INNER JOIN sys.tables AS tbl ON tbl.object_id = i.object_id");
            sb.AppendLine(@"INNER JOIN sys.index_columns AS c ON c.object_id = tbl.object_id AND c.index_id = i.index_id");
            sb.AppendLine(@"INNER JOIN sys.columns AS f ON f.object_id = tbl.object_id AND f.column_id = c.column_id");
            sb.AppendLine(@"INNER JOIN sys.types AS t ON t.system_type_id = f.system_type_id");
            sb.AppendLine(@"WHERE t.system_type_id = t.user_type_id");
            sb.AppendLine(@"  AND tbl.object_id = OBJECT_ID(@tableName) AND i.type IN (1, 2)");
            sb.AppendLine(@"ORDER BY");
            sb.AppendLine(@"  i.index_id    ASC,");
            sb.AppendLine(@"  c.key_ordinal ASC;");

            return sb.ToString();
        }
        public static List<IndexInfo> GetIndexes(string connectionString, string tableName)
        {
            List<IndexInfo> list = new List<IndexInfo>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = GetSelectIndexesScript();
                    command.Parameters.AddWithValue("tableName", tableName);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        int current_id = -1;
                        IndexInfo index = null;
                        IndexColumnInfo column = null;

                        while (reader.Read())
                        {
                            int index_id = reader.GetInt32("IndexId");

                            if (current_id != index_id)
                            {
                                index = new IndexInfo(
                                    reader.GetString("IndexName"),
                                    reader.GetBoolean("IsUnique"),
                                    reader.GetByte("IsClustered") == 1,
                                    reader.GetBoolean("IsPrimaryKey"));
                                list.Add(index);

                                current_id = index_id;
                            }

                            column = new IndexColumnInfo(
                                reader.GetString("ColumnName"),
                                reader.GetString("TypeName"),
                                reader.GetByte("KeyOrdinal"),
                                reader.GetBoolean("IsIncluded"),
                                reader.GetBoolean("IsNullable"),
                                reader.GetBoolean("IsDescending"));
                            
                            if (column.IsIncluded)
                            {
                                index.Includes.Add(column);
                            }
                            else
                            {
                                index.Columns.Add(column);
                            }
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }

        public static byte[] Get1CUuid(byte[] uuid_sql)
        {
            // CAST(REVERSE(SUBSTRING(@uuid_sql, 9, 8)) AS binary(8)) + SUBSTRING(@uuid_sql, 1, 8)

            byte[] uuid_1c = new byte[16];

            for (int i = 0; i < 8; i++)
            {
                uuid_1c[i] = uuid_sql[15 - i];
                uuid_1c[8 + i] = uuid_sql[i];
            }

            return uuid_1c;
        }
        public static byte[] GetSqlUuid(byte[] uuid_1c)
        {
            byte[] uuid_sql = new byte[16];

            for (int i = 0; i < 8; i++)
            {
                uuid_sql[i] = uuid_1c[8 + i];
                uuid_sql[8 + i] = uuid_1c[7 - i];
            }

            return uuid_sql;
        }
        public static byte[] GetSqlUuid(Guid guid_1c)
        {
            return GetSqlUuid(guid_1c.ToByteArray());
        }

        private const string TABLE_EXISTS_SCRIPT = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{0}';";
        private const string TABLE_SELECT_SCRIPT = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_NAME ASC;";
        public static string GetTableExistsScript(in string tableName)
        {
            return string.Format(TABLE_EXISTS_SCRIPT, tableName);
        }
        public static string GetTableSelectScript()
        {
            return TABLE_SELECT_SCRIPT;
        }
    }
}