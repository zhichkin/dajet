using DaJet.Metadata;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public interface ISqlTranspiler
    {
        int YearOffset { get; set; }
        void Visit(in SyntaxNode expression, in StringBuilder script);
        bool TryTranspile(in ScriptModel model, in IMetadataProvider metadata, out TranspilerResult result, out string error);
    }
}