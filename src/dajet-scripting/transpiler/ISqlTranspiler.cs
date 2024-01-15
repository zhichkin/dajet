using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public interface ISqlTranspiler
    {
        int YearOffset { get; set; }
        bool TryTranspile(in ScriptModel model, in IMetadataProvider metadata, out TranspilerResult result, out string error);
    }
}