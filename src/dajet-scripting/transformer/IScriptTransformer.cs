using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public interface IScriptTransformer
    {
        public void Transform(in SyntaxNode node);
    }
}