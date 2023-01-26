using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class ScriptTransformer : IScriptVisitor
    {
        public ScriptTransformer()
        {
            WhereOnClauseTransformer transformer1 = new();
            BooleanOperatorTransformer transformer2 = new();

            Transformers.Add(typeof(OnClause), transformer1);
            Transformers.Add(typeof(WhereClause), transformer1);
            Transformers.Add(typeof(WhenExpression), transformer1);
            Transformers.Add(typeof(BooleanUnaryOperator), transformer2);
            Transformers.Add(typeof(BooleanBinaryOperator), transformer2);
            Transformers.Add(typeof(BooleanGroupExpression), transformer2);
        }
        public Dictionary<Type, IScriptVisitor> Transformers = new();
        public bool TryTransform(in SyntaxNode tree, out string error)
        {
            error = string.Empty;

            try
            {
                ScriptWalker.Walk(in tree, this);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return string.IsNullOrWhiteSpace(error);
        }
        public void SayHello(SyntaxNode node)
        {
            if (node == null)
            {
                return;
            }

            if (Transformers.TryGetValue(node.GetType(), out IScriptVisitor visitor))
            {
                visitor?.SayHello(node);
            }
        }
        public void SayGoodbye(SyntaxNode node)
        {
            if (node == null)
            {
                return;
            }

            if (Transformers.TryGetValue(node.GetType(), out IScriptVisitor visitor))
            {
                visitor?.SayGoodbye(node);
            }
        }
    }
}