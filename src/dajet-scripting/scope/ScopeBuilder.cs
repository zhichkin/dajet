using DaJet.Metadata;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class ScopeBuilder : IScriptVisitor
    {
        private ScriptScope _scope = null!;
        private ScriptScope _current = null!;
        public bool TryBuild(in ScriptModel model, out ScriptScope scope, out string error)
        {
            scope = null!;
            error = string.Empty;

            try
            {
                ScriptWalker.Walk(model, this);

                scope = _scope;
                _scope = null!;
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return (scope != null);
        }
        public void SayHello(SyntaxNode node)
        {
            if (node == null)
            {
                return;
            }

            if (node is ScriptModel)
            {
                OpenScope(ScopeType.Global, node);
            }
            else if (node is SelectStatement)
            {
                OpenScope(ScopeType.Root, node);
            }
            else if (node is TableJoinOperator)
            {
                OpenScope(ScopeType.Node, node);
            }
            else if (node is CommonTableExpression)
            {
                OpenScope(ScopeType.Node, node);
            }
            else if (node is SubqueryExpression)
            {
                JoinScope(node);
            }
            else if (node is Identifier)
            {
                JoinScope(node);
            }
            else if (node is DeclareStatement)
            {
                JoinScope(node);
            }
        }
        public void SayGoodbye(SyntaxNode node)
        {
            if (node == null)
            {
                return;
            }
            
            if (node is ScriptModel)
            {
                CloseScope(node);
            }
            else if (node is SelectStatement)
            {
                CloseScope(node);
            }
            else if (node is TableJoinOperator)
            {
                CloseScope(node);
            }
            else if (node is CommonTableExpression)
            {
                CloseScope(node);
            }
        }
        private void OpenScope(ScopeType type, SyntaxNode owner)
        {
            if (_scope == null)
            {
                _scope = new ScriptScope(type, owner, null!);
                _current = _scope;
            }
            else
            {
                ScriptScope scope = new(type, owner, _current);
                _current.Children.Add(scope);
                _current = scope;
            }
        }
        private void CloseScope(SyntaxNode node)
        {
            if (_current != null)
            {
                _current = _current.Parent;
            }
        }
        private void JoinScope(SyntaxNode node)
        {
            if (_current != null)
            {
                _current.Identifiers.Add(node);
            }
        }
    }
}