using DaJet.Metadata.Model;
using System.Text;

namespace DaJet.Data.Client
{
    internal static class ScriptGenerator
    {
        internal static string GenerateSelectEntityScript(in ChangeTrackingTable table)
        {
            if (table.Entity is not InformationRegister register) { return string.Empty; }

            StringBuilder script = new();
            for (int i = 0; i < table.Properties.Count; i++)
            {
                MetadataProperty property = table.Properties[i];

                if (property.Name == "УзелОбмена" || property.Name == "НомерСообщения")
                {
                    continue;
                }

                string type = GetDataTypeLiteral(in property);

                script.AppendLine($"DECLARE @{property.Name} {type};");
            }
            script.AppendLine();
            script.AppendLine("SELECT");
            int line = 0;
            for (int i = 0; i < register.Properties.Count; i++)
            {
                MetadataProperty property = register.Properties[i];

                if (line > 0) { script.AppendLine(","); }

                script.Append($"{property.Name}"); line++;
            }
            script.AppendLine();
            script.AppendLine($"FROM РегистрСведений.{register.Name}");
            line = 0;
            for (int i = 0; i < table.Properties.Count; i++)
            {
                MetadataProperty property = table.Properties[i];

                if (property.Name == "УзелОбмена" || property.Name == "НомерСообщения")
                {
                    continue;
                }

                if (line == 0) { script.Append("WHERE "); }
                else { script.Append("AND "); }

                script.AppendLine($"{property.Name} = @{property.Name}"); line++;
            }

            return script.ToString();
        }
        private static string GetDataTypeLiteral(in MetadataProperty property)
        {
            string type = property.PropertyType.GetDataTypeLiteral();

            if (type == "entity" || type == "union")
            {
                type = "uuid";
            }

            return type;
        }

        internal static string GenerateProducerScript()
        {
            StringBuilder script = new();

            script.AppendLine("DECLARE @uuid uuid     = '00000000-0000-0000-0000-000000000000';");
            script.AppendLine("DECLARE @type string   = 'message-type';");
            script.AppendLine("DECLARE @body string   = 'message-body';");
            script.AppendLine("DECLARE @time datetime = '2025-08-01T12:34:56';");
            script.AppendLine();
            script.AppendLine("CREATE SEQUENCE so_incoming_queue;");
            script.AppendLine();
            script.AppendLine("CREATE COMPUTED TABLE source AS");
            script.AppendLine("(");
            script.AppendLine("  SELECT VECTOR('so_incoming_queue')       AS НомерСообщения,");
            script.AppendLine("         @uuid AS Идентификатор,     @type AS ТипСообщения,");
            script.AppendLine("         @time AS ОтметкаВремени,    @body AS ТелоСообщения");
            script.AppendLine(")");
            script.AppendLine("INSERT РегистрСведений.ВходящаяОчередь FROM source");
            script.AppendLine();
            script.AppendLine("SELECT TOP 1 НомерСообщения, ТипСообщения, ТелоСообщения, ОтметкаВремени");
            script.AppendLine("  FROM РегистрСведений.ВходящаяОчередь ORDER BY НомерСообщения DESC");

            return script.ToString();
        }
    }
}