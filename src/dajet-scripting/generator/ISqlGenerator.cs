using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public interface ISqlGenerator
    {
        int YearOffset { get; set; }
        bool TryGenerate(in ScriptModel model, in IMetadataProvider metadata, out GeneratorResult result);
        bool TryGenerate(in ScriptModel model, in IMetadataProvider metadata, out List<ScriptCommand> commands, out string error);
    }
}