using DaJet.DbViewGenerator;
using DaJet.Metadata.Model;
using System.Text;

namespace DaJet.Metadata.Services
{
    public sealed class PgDbViewGenerator : DbViewGenerator
    {
        public PgDbViewGenerator(DbViewGeneratorOptions options) : base(options) { }

        #region "SQL COMMANDS"

        protected override string DEFAULT_SCHEMA_NAME { get { return "public"; } } // default PostgreSQL schema
        protected override string DROP_VIEW_SCRIPT { get { return "DROP VIEW IF EXISTS {0};"; } }
        protected override string SELECT_VIEWS_SCRIPT
        {
            get
            {
                return
                    "SELECT table_schema, table_name " +
                    "FROM information_schema.views " +
                    "WHERE table_schema = '{0}';";
            }
        }
        protected override string SCHEMA_EXISTS_SCRIPT
        {
            get { return "SELECT 1 FROM information_schema.schemata WHERE schema_name = '{0}';"; }
        }
        protected override string CREATE_SCHEMA_SCRIPT { get { return "CREATE SCHEMA {0};"; } }
        protected override string DROP_SCHEMA_SCRIPT { get { return "DROP SCHEMA {0};"; } }
        protected override string SELECT_SCHEMA_SCRIPT
        {
            get
            {
                return
                    "SELECT nspname FROM pg_catalog.pg_namespace " +
                    "WHERE nspname NOT LIKE 'pg_%' " +
                    "AND nspname NOT IN ('pg_catalog', 'information_schema');";
            }
        }

        #endregion

        protected override string FormatViewName(string viewName)
        {
            return $"{_options.Schema}.\"{viewName}\"";
        }
        public override string GenerateViewScript(in ApplicationObject metadata, string viewName)
        {
            bool isTablePart = (metadata is TablePart);

            StringBuilder script = new();
            StringBuilder fields = new();

            script.AppendLine($"CREATE VIEW {_options.Schema}.\"{viewName}\" AS SELECT");

            foreach (MetadataProperty property in metadata.Properties)
            {
                foreach (MetadataColumn field in property.Columns)
                {

                    if (fields.Length > 0) { fields.Append(','); }

                    fields.AppendLine($"{field.Name} AS \"{Configurator.CreateColumnAlias(in property, in field)}\"");
                }
            }

            script.Append(fields);

            script.Append($"FROM {metadata.TableName};");

            return script.ToString();
        }
        public override string GenerateEnumViewScript(in Enumeration enumeration, string viewName)
        {
            StringBuilder script = new();
            StringBuilder fields = new();

            script.AppendLine($"CREATE VIEW {_options.Schema}.\"{viewName}\" AS");

            script.AppendLine("SELECT e._EnumOrder AS \"Порядок\", t.\"Имя\", t.\"Синоним\", t.\"Значение\"");
            script.AppendLine($"FROM {enumeration.TableName} AS e INNER JOIN");
            script.AppendLine("(");

            foreach (EnumValue value in enumeration.Values)
            {
                if (fields.Length > 0)
                {
                    fields.AppendLine("UNION ALL");
                }

                string uuid = value.Uuid.ToString("N");

                uuid =
                    uuid.Substring(16, 16) +
                    uuid.Substring(12, 4) +
                    uuid.Substring(8, 4) +
                    uuid.Substring(0, 8);

                fields.Append("SELECT ");
                fields.Append($"CAST('{value.Name}' AS mvarchar) AS \"Имя\", ");
                fields.Append($"CAST('{value.Alias}' AS mvarchar) AS \"Синоним\", ");
                fields.AppendLine($"CAST(E'\\\\x{uuid}' AS bytea) AS \"Значение\"");
            }

            script.Append(fields);
            script.Append(") AS t ON e._IDRRef = t.\"Значение\";");

            return script.ToString();
        }
    }
}