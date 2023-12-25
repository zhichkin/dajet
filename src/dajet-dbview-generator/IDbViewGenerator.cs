using DaJet.Metadata.Model;

namespace DaJet.Metadata.Services
{
    public interface IDbViewGenerator
    {
        DbViewGeneratorOptions Options { get; }
        
        List<string> SelectSchemas();
        bool SchemaExists(string name);
        void CreateSchema(string name);
        void DropSchema(string name);

        List<string> SelectViews(string schema);

        string GenerateViewScript(in ApplicationObject metadata, string viewName);
        string GenerateEnumViewScript(in Enumeration enumeration, string viewName);

        bool TryScriptView(in ApplicationObject metadata, in StreamWriter writer, out string error);
        bool TryScriptViews(in OneDbMetadataProvider cache, in StreamWriter writer, out string error);

        bool TryCreateView(in ApplicationObject metadata, out string error);
        bool TryCreateViews(in OneDbMetadataProvider cache, out int result, out List<string> errors);
        
        int DropViews();
        void DropView(in ApplicationObject metadata);
    }

    // SQL Server: SELECT OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[Перечисление.СтавкиНДС]', 'V'));
    // PostgreSQL: SELECT pg_get_viewdef('public."Документ.Документ1"'::regclass, true);
}