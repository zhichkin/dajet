using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public interface IUserDefinedFunction
    {
        Type ReturnType { get; }
        void Transpile(in ISqlTranspiler owner, in FunctionExpression node, in StringBuilder script);
    }
}