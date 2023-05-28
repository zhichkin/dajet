using Npgsql;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DaJet.Data
{
    public interface ISqlHelper
    {
        List<IndexInfo> GetIndexes(string connectionString, string tableName);
    }
    public sealed class PgSqlHelper : ISqlHelper
    {
        private string GetSelectIndexesScript()
        {
            StringBuilder script = new();

            script.AppendLine("SELECT");
            script.AppendLine("ic.oid       AS index_oid,");
            script.AppendLine("ic.relname   AS index_name,");
            script.AppendLine("c.ordinality AS column_ordinal,");
            script.AppendLine("a.attname    AS column_name,");
            script.AppendLine("format_type(a.atttypid, a.atttypmod) AS column_type,");
            script.AppendLine("CASE WHEN a.attnotnull THEN false ELSE true END AS is_nullable,");
            script.AppendLine("CASE o.option & 1 WHEN 1 THEN false ELSE true END AS is_ascending,");
            script.AppendLine("ix.indisunique    AS is_unique,");
            script.AppendLine("ix.indisprimary   AS is_primary,");
            script.AppendLine("ix.indisclustered AS is_clustered");
            script.AppendLine("FROM pg_index AS ix");
            script.AppendLine("INNER JOIN pg_class AS ic ON ic.oid = ix.indexrelid");
            script.AppendLine("INNER JOIN pg_class AS tc ON tc.oid = ix.indrelid AND tc.relname = @table_name");
            script.AppendLine("INNER JOIN pg_namespace AS ns ON tc.relnamespace = ns.oid AND ns.nspname = 'public'");
            script.AppendLine("CROSS JOIN LATERAL unnest (ix.indkey) WITH ORDINALITY AS c (colnum, ordinality)");
            script.AppendLine("LEFT JOIN LATERAL unnest (ix.indoption) WITH ORDINALITY AS o (option, ordinality)");
            script.AppendLine("ON c.ordinality = o.ordinality");
            script.AppendLine("INNER JOIN pg_attribute AS a ON tc.oid = a.attrelid AND a.attnum = c.colnum");
            script.AppendLine("ORDER BY ic.oid ASC, c.ordinality ASC");

            return script.ToString();
        }
        public List<IndexInfo> GetIndexes(string connectionString, string tableName)
        {
            List<IndexInfo> list = new();

            using (NpgsqlConnection connection = new(connectionString))
            {
                connection.Open();

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = GetSelectIndexesScript();
                    command.Parameters.AddWithValue("table_name", tableName);

                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        uint current_oid = 0;
                        IndexInfo index = null;
                        IndexColumnInfo column = null;

                        while (reader.Read())
                        {
                            uint index_oid = (uint)reader.GetValue("index_oid");

                            if (current_oid != index_oid)
                            {
                                index = new IndexInfo(
                                    reader.GetString("index_name"),
                                    reader.GetBoolean("is_unique"),
                                    reader.GetBoolean("is_primary"),
                                    reader.GetBoolean("is_clustered"));

                                list.Add(index);

                                current_oid = index_oid;
                            }

                            column = new IndexColumnInfo(
                                reader.GetString("column_name"),
                                reader.GetString("column_type"),
                                (byte)reader.GetInt64("column_ordinal"),
                                false,
                                reader.GetBoolean("is_nullable"),
                                !reader.GetBoolean("is_ascending"));

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