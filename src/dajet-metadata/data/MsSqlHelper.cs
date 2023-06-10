using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DaJet.Data
{
    public sealed class MsSqlHelper : ISqlHelper
    {
        private string GetSelectIndexesScript()
        {
            StringBuilder script = new();

            script.AppendLine(@"SELECT");
            script.AppendLine(@"i.index_id AS index_id,");
            script.AppendLine(@"i.name AS index_name,");
            script.AppendLine(@"ic.key_ordinal AS column_ordinal,");
            script.AppendLine(@"c.name AS column_name,");
            script.AppendLine(@"[column_type] = CASE");
            script.AppendLine(@"WHEN dt.[name] IN ('varchar', 'char', 'binary', 'varbinary') THEN dt.[name] + '(' + IIF(c.max_length = -1, 'max', CAST(c.max_length AS VARCHAR(25))) + ')'");
            script.AppendLine(@"WHEN dt.[name] IN ('nvarchar', 'nchar') THEN dt.[name] + '(' + IIF(c.max_length = -1, 'max', CAST(c.max_length / 2 AS VARCHAR(25))) + ')'");
            script.AppendLine(@"WHEN dt.[name] IN ('decimal', 'numeric') THEN dt.[name] + '(' + CAST(c.[precision] AS VARCHAR(25)) + ',' + CAST(c.[scale] AS VARCHAR(25)) + ')'");
            script.AppendLine(@"ELSE dt.[name] END,");
            script.AppendLine(@"c.is_nullable AS is_nullable,");
            script.AppendLine(@"ic.is_descending_key AS is_descending,");
            script.AppendLine(@"i.is_unique AS is_unique,");
            script.AppendLine(@"i.is_primary_key AS is_primary,");
            script.AppendLine(@"CASE WHEN i.type = 1 THEN CAST(0x01 AS bit) ELSE CAST(0x00 AS bit) END AS is_clustered");
            script.AppendLine(@"FROM sys.indexes AS i");
            script.AppendLine(@"INNER JOIN sys.tables AS t ON t.object_id = i.object_id");
            script.AppendLine(@"INNER JOIN sys.index_columns AS ic ON ic.object_id = t.object_id AND ic.index_id = i.index_id");
            script.AppendLine(@"INNER JOIN sys.columns AS c ON c.object_id = t.object_id AND c.column_id = ic.column_id");
            script.AppendLine(@"INNER JOIN sys.types dt ON dt.system_type_id = dt.user_type_id AND c.user_type_id = dt.user_type_id");
            script.AppendLine(@"WHERE t.object_id = OBJECT_ID(@table_name) AND i.type = 1"); // CLUSTERED
            script.AppendLine(@"ORDER BY i.index_id ASC, ic.key_ordinal ASC;");
            
            return script.ToString();
        }
        public List<IndexInfo> GetIndexes(string connectionString, string tableName)
        {
            List<IndexInfo> list = new();

            using (SqlConnection connection = new(connectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = GetSelectIndexesScript();
                    command.Parameters.AddWithValue("table_name", tableName);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        int current_id = 0;
                        IndexInfo index = null;
                        IndexColumnInfo column = null;

                        while (reader.Read())
                        {
                            int index_id = (int)reader.GetValue("index_id");

                            if (current_id != index_id)
                            {
                                index = new IndexInfo(
                                    reader.GetString("index_name"),
                                    reader.GetBoolean("is_unique"),
                                    reader.GetBoolean("is_primary"),
                                    reader.GetBoolean("is_clustered"));

                                list.Add(index);

                                current_id = index_id;
                            }

                            column = new IndexColumnInfo(
                                reader.GetString("column_name"),
                                reader.GetString("column_type"),
                                reader.GetByte("column_ordinal"),
                                false,
                                reader.GetBoolean("is_nullable"),
                                reader.GetBoolean("is_descending"));

                            index.Columns.Add(column);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
    }
}