using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public interface ISqlGenerator
    {
        bool TryGenerate(in ScriptModel model, out GeneratorResult result);
    }
}