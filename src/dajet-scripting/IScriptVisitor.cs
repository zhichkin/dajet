using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public interface IScriptVisitor
    {
        void SayHello(SyntaxNode node);
        void SayGoodbye(SyntaxNode node);
    }
}