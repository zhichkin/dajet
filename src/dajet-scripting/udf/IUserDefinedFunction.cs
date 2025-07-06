using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    public interface IUserDefinedFunction
    {
        Type GetReturnType(in FunctionExpression node);
        FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in FunctionExpression node, in StringBuilder script);
    }
}