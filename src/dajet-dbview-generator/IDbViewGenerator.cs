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
        bool TryScriptViews(in MetadataCache cache, in StreamWriter writer, out string error);

        bool TryCreateView(in ApplicationObject metadata, out string error);
        bool TryCreateViews(in MetadataCache cache, out int result, out List<string> errors);
        
        int DropViews();
        void DropView(in ApplicationObject metadata);
    }
}