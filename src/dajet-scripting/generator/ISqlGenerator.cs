using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public interface ISqlGenerator
    {
        int YearOffset { get; set; }
        bool TryGenerate(in ScriptModel model, out GeneratorResult result);
    }
}