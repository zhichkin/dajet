using DaJet.Data;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    [Function(UDF_JSON.Name)]
    public sealed class UDF_JSON : IUserDefinedFunction
    {
        public const string Name = "JSON";
        public Type ReturnType { get { return typeof(string); } }
        public FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Name != UDF_JSON.Name)
            {
                throw new FormatException($"[JSON] invalid mapping {node.Name}");
            }

            if (node.Parameters.Count == 0)
            {
                throw new FormatException("[JSON] parameter missing");
            }

            if (node.Parameters.Count > 1)
            {
                throw new FormatException("[JSON] too many parameters");
            }

            FunctionDescriptor descriptor;

            SyntaxNode parameter = node.Parameters[0];

            if (parameter is VariableReference variable)
            {
                descriptor = Transpile(in transpiler, in variable, in script);
            }
            else if (parameter is MemberAccessExpression accessor)
            {
                descriptor = Transpile(in transpiler, in accessor, in script);
            }
            else
            {
                throw new FormatException("[JSON] invalid parameter type");
            }

            if (descriptor is not null)
            {
                descriptor.Node = node;
            }

            return descriptor;
        }
        private FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in VariableReference variable, in StringBuilder script)
        {
            if (variable.Token != TokenType.Object || variable.Token != TokenType.Array)
            {
                throw new FormatException("[JSON] invalid variable type");
            }

            // type != typeof(DataObject) || type != typeof(List<DataObject>)

            string parameterName = $"@JSON_" + variable.Identifier[1..];

            script.Append(parameterName);

            FunctionDescriptor descriptor = new()
            {
                Target = parameterName,
                ReturnType = ReturnType
            };

            return descriptor;
        }
        private FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in MemberAccessExpression accessor, in StringBuilder script)
        {
            if (accessor.Binding is not Type type)
            {
                throw new FormatException("[JSON] invalid property binding");
            }

            if (type != typeof(DataObject) || type != typeof(List<DataObject>))
            {
                throw new FormatException("[JSON] invalid property type");
            }

            string parameterName = $"@JSON_" + accessor.Identifier[1..].Replace('.', '_');

            script.Append(parameterName);

            FunctionDescriptor descriptor = new()
            {
                Target = parameterName,
                ReturnType = ReturnType
            };

            return descriptor;
        }
    }
}