using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public interface IUserDefinedFunction
    {
        Type ReturnType { get; }
        FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in FunctionExpression node, in StringBuilder script);
    }
}