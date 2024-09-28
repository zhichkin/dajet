using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public interface ISqlTranspiler
    {
        DatabaseProvider Target { get; }
        int YearOffset { get; set; }
        void Visit(in SyntaxNode expression, in StringBuilder script);
        bool TryTranspile(in ScriptModel model, in IMetadataProvider metadata, out TranspilerResult result, out string error);
    }
}