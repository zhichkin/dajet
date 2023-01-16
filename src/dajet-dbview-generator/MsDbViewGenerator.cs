using DaJet.DbViewGenerator;
using DaJet.Metadata.Model;
using System.Text;

namespace DaJet.Metadata.Services
{
    public sealed class MsDbViewGenerator : DbViewGenerator
    {
        public MsDbViewGenerator(DbViewGeneratorOptions options) : base(options) { }

        #region "SQL COMMANDS"

        protected override string DEFAULT_SCHEMA_NAME { get { return "dbo"; } } // default SQL Server schema
        protected override string DROP_VIEW_SCRIPT { get { return "IF OBJECT_ID(N'{0}', N'V') IS NOT NULL DROP VIEW {0};"; } }
        protected override string SELECT_VIEWS_SCRIPT
        {
            get
            {
                return
                    "SELECT s.name AS [Schema], v.name AS [View]" +
                    "FROM sys.views AS v " +
                    "INNER JOIN sys.schemas AS s " +
                    "ON v.schema_id = s.schema_id AND is_ms_shipped = 0 AND s.name = N'{0}';";
            }
        }
        protected override string SCHEMA_EXISTS_SCRIPT { get { return "SELECT 1 FROM sys.schemas WHERE name = N'{0}';"; } }
        protected override string CREATE_SCHEMA_SCRIPT { get { return "CREATE SCHEMA {0};"; } }
        protected override string DROP_SCHEMA_SCRIPT { get { return "DROP SCHEMA {0};"; } }
        protected override string SELECT_SCHEMA_SCRIPT
        {
            get
            {
                return
                    "SELECT s.name FROM sys.schemas s " +
                    "INNER JOIN sys.database_principals p " +
                    "ON s.principal_id = p.principal_id " +
                    "AND p.is_fixed_role = 0 " +
                    "AND s.name NOT IN (N'sys', N'guest',N'INFORMATION_SCHEMA');";
            }
        }

        #endregion

        protected override string FormatViewName(string viewName)
        {
            return $"[{_options.Schema}].[{viewName}]";
        }
        public override string GenerateViewScript(in ApplicationObject metadata, string viewName)
        {
            bool isTablePart = (metadata is TablePart);

            StringBuilder script = new();
            StringBuilder fields = new();

            script.AppendLine($"CREATE VIEW [{_options.Schema}].[{viewName}] AS SELECT");

            foreach (MetadataProperty property in metadata.Properties)
            {
                foreach (MetadataColumn field in property.Columns)
                {
                    if (fields.Length > 0)
                    {
                        fields.Append(',');
                    }

                    fields.AppendLine($"{field.Name} AS [{Configurator.CreateColumnAlias(in property, in field)}]");
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

            script.AppendLine($"CREATE VIEW [{_options.Schema}].[{viewName}] AS");

            script.AppendLine("SELECT e._EnumOrder AS [Порядок], t.[Имя], t.[Синоним], t.[Значение]");
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
                fields.Append($"N'{value.Name}' AS [Имя], ");
                fields.Append($"N'{value.Alias}' AS [Синоним], ");
                fields.AppendLine($"0x{uuid} AS [Значение]");
            }

            script.Append(fields);
            script.Append(") AS t ON e._IDRRef = t.[Значение];");

            return script.ToString();
        }
    }
}