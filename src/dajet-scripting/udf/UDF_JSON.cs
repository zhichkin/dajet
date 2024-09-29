using DaJet.Data;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    [Function(UDF_JSON.Name)]
    public sealed class UDF_JSON : IUserDefinedFunction
    {
        public const string Name = "JSON";
        private DatabaseProvider _target;
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

            _target = transpiler.Target;

            FunctionDescriptor descriptor;

            SyntaxNode parameter = node.Parameters[0];

            if (parameter is VariableReference variable)
            {
                descriptor = Transpile(in transpiler, in variable, in script);
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
            if (variable.Binding is not TypeIdentifier type)
            {
                throw new FormatException("[JSON] invalid variable binding");
            }

            if (!(type.Token == TokenType.Object || type.Token == TokenType.Array))
            {
                throw new FormatException("[JSON] invalid variable type");
            }

            string parameterName = $"@JSON_" + variable.Identifier[1..];

            if (_target == DatabaseProvider.PostgreSql)
            {
                script.Append("CAST(").Append(parameterName).Append(" AS mvarchar)");
            }
            else
            {
                script.Append(parameterName);
            }

            FunctionDescriptor descriptor = new()
            {
                Target = parameterName,
                ReturnType = ReturnType
            };

            return descriptor;
        }
    }
}