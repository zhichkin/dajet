using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class ScriptTransformer : IScriptWalker
    {
        public ScriptTransformer()
        {
            BooleanClauseTransformer booleanTransformer = new();
            Transformers.Add(typeof(OnClause), booleanTransformer);
            Transformers.Add(typeof(WhenClause), booleanTransformer);
            Transformers.Add(typeof(WhereClause), booleanTransformer);
            Transformers.Add(typeof(HavingClause), booleanTransformer);

            ColumnReferenceTransformer columnTransformer = new();
            Transformers.Add(typeof(OrderClause), columnTransformer);
            Transformers.Add(typeof(GroupClause), columnTransformer);
            Transformers.Add(typeof(ColumnExpression), columnTransformer);
            Transformers.Add(typeof(ColumnReference), columnTransformer);
        }
        public Dictionary<Type, IScriptTransformer> Transformers = new();
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
            if (node == null) { return; }

            if (Transformers.TryGetValue(node.GetType(), out IScriptTransformer transformer))
            {
                transformer?.Transform(in node);
            }
        }
        public void SayGoodbye(SyntaxNode node)
        {
            return; // not implemented
        }
    }
}