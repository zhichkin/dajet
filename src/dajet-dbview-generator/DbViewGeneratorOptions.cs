using DaJet.Data;

namespace DaJet.Metadata.Services
{
    public sealed class DbViewGeneratorOptions
    {
        public string Schema { get; set; } = string.Empty;
        public bool Codify { get; set; } = false; // shorten view names for PostgreSql
        public string OutputFile { get; set; } = string.Empty;
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.SqlServer;
        public string ConnectionString { get; set; } = string.Empty;
        public List<string> MetadataTypes { get; set; } = new()
        {
            "Документ",
            "Справочник",
            "ПланОбмена",
            "Перечисление",
            "РегистрСведений",
            "РегистрНакопления",
            "ПланВидовХарактеристик",
            "ПланСчетов",
            "РегистрБухгалтерии"
        };
        public static void Configure(in DbViewGeneratorOptions options, Dictionary<string, string> values)
        {
            if (values.TryGetValue(nameof(DbViewGeneratorOptions.DatabaseProvider), out string? DatabaseProvider)
                && !string.IsNullOrWhiteSpace(DatabaseProvider)
                && Enum.TryParse(DatabaseProvider, out DatabaseProvider provider))
            {
                options.DatabaseProvider = provider;
            }

            if (values.TryGetValue(nameof(DbViewGeneratorOptions.ConnectionString), out string? ConnectionString)
                && !string.IsNullOrWhiteSpace(ConnectionString))
            {
                options.ConnectionString = ConnectionString ?? string.Empty;
            }

            if (values.TryGetValue(nameof(DbViewGeneratorOptions.Schema), out string? Schema)
                && !string.IsNullOrWhiteSpace(Schema))
            {
                options.Schema = Schema ?? string.Empty;
            }

            if (values.TryGetValue(nameof(DbViewGeneratorOptions.OutputFile), out string? OutputFile)
                && !string.IsNullOrWhiteSpace(OutputFile))
            {
                options.OutputFile = OutputFile ?? string.Empty;
            }

            if (values.TryGetValue(nameof(DbViewGeneratorOptions.Codify), out string? Codify)
                && !string.IsNullOrWhiteSpace(Codify))
            {
                options.Codify = (Codify == "true");
            }
        }
    }
}