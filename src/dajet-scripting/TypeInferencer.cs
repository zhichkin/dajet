using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class TypeInferencer : IScriptVisitor
    {
        private Type _type = null;
        public Type InferOrDefault(in SyntaxNode node)
        {
            if (TryInferType(in node, out Type type, out string _))
            {
                return type ?? typeof(decimal);
            }
            return typeof(decimal);
        }
        public bool TryInferType(in SyntaxNode node, out Type type, out string error)
        {
            type = null;
            error = string.Empty;

            try
            {
                ScriptWalker.Walk(in node, this);

                type = _type;
                _type = null;
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return (type != null);
        }
        public void SayGoodbye(SyntaxNode node) { /* not used */ }
        public void SayHello(SyntaxNode node)
        {
            if (_type is not null) { return; }

            if (node is ScalarExpression scalar)
            {
                VisitScalarExpression(in scalar);
            }
        }
        private void VisitCaseExpression(CaseExpression expression)
        {
            //TODO: !!!
        }
        private void VisitScalarExpression(in ScalarExpression scalar)
        {
            if (scalar.Token == TokenType.Boolean)
            {
                _type = typeof(bool);
            }
            else if (scalar.Token == TokenType.Number)
            {
                _type = typeof(decimal);
            }
            else if (scalar.Token == TokenType.DateTime)
            {
                _type = typeof(DateTime);
            }
            else if (scalar.Token == TokenType.String)
            {
                _type = typeof(string);
            }
            else if (scalar.Token == TokenType.Binary)
            {
                _type = typeof(byte[]);
            }
            else if (scalar.Token == TokenType.Entity)
            {
                //_type = typeof(Entity);
            }
            else if (scalar.Token == TokenType.NULL)
            {
                //_type = typeof(Union); // undefined
            }
        }
    }
}