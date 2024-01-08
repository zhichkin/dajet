using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class ScopeBuilder : IScriptWalker
    {
        private ScriptScope _scope = null; // root scope owned by ScriptModel
        private ScriptScope _current = null;
        public bool TryBuild(in ScriptModel model, out ScriptScope scope, out string error)
        {
            scope = null;
            error = string.Empty;

            try
            {
                ScriptWalker.Walk(model, this);

                scope = _scope;
                _scope = null;
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return (scope != null);
        }
        public void SayHello(in SyntaxNode node)
        {
            if (node == null) { return; }

            if (node is ScriptModel) { OpenScope(node); }
            else if (node is DeclareStatement) { JoinScope(node); }
            else if (node is TableVariableExpression) { OpenScope(node); }
            else if (node is TemporaryTableExpression) { OpenScope(node); }
            else if (node is SelectStatement) { OpenScope(node); }
            else if (node is InsertStatement) { OpenScope(node); }
            else if (node is UpdateStatement) { OpenScope(node); }
            else if (node is DeleteStatement) { OpenScope(node); }
            else if (node is UpsertStatement) { OpenScope(node); }
            else if (node is ConsumeStatement) { OpenScope(node); }
            else if (node is CommonTableExpression) { OpenScope(node); }
            else if (node is TableExpression) { OpenScope(node); }
            else if (node is SelectExpression) { OpenScope(node); }
            else if (node is TableJoinOperator) { OpenScope(node); }
            else if (node is TableUnionOperator) { OpenScope(node); }
            else if (node is TableReference) { JoinScope(node); }
            else if (node is ColumnReference) { JoinScope(node); }
            else if (node is TypeIdentifier) { JoinGlobalScope(node); }
            else if (node is VariableReference) { JoinGlobalScope(node); }
            else if (node is IntoClause into) { CreateVirtualTableExpression(in into); }
        }
        public void SayGoodbye(in SyntaxNode node)
        {
            if (node == null) { return; }
            
            if (node is ScriptModel) { CloseScope(); }
            else if (node is SelectStatement) { CloseScope(); }
            else if (node is InsertStatement) { CloseScope(); }
            else if (node is UpdateStatement) { CloseScope(); }
            else if (node is DeleteStatement) { CloseScope(); }
            else if (node is UpsertStatement) { CloseScope(); }
            else if (node is ConsumeStatement) { CloseScope(); }
            else if (node is CommonTableExpression) { CloseScope(); }
            else if (node is SelectExpression) { CloseScope(); }
            else if (node is TableExpression) { CloseScope(); }
            else if (node is TableJoinOperator) { CloseScope(); }
            else if (node is TableUnionOperator) { CloseScope(); }
            else if (node is TableVariableExpression) { CloseScope(); }
            else if (node is TemporaryTableExpression) { CloseScope(); }
        }
        private void OpenScope(SyntaxNode owner)
        {
            if (_scope == null)
            {
                _scope = new ScriptScope(owner, null);
                _current = _scope;
            }
            else
            {
                ScriptScope scope = new(owner, _current);
                _current.Children.Add(scope);
                _current = scope;
            }
        }
        private void JoinScope(SyntaxNode node)
        {
            if (_current != null)
            {
                _current.Identifiers.Add(node);
            }
        }
        private void JoinGlobalScope(SyntaxNode node)
        {
            if (_scope != null)
            {
                _scope.Identifiers.Add(node);
            }
        }
        private void CloseScope()
        {
            if (_current != null)
            {
                _current = _current.Parent;
            }
        }
        private void CreateVirtualTableExpression(in IntoClause target)
        {
            SyntaxNode table;

            if (_current is not null && _current.Owner is ConsumeStatement)
            {
                table = new TableVariableExpression() // MS SQL Server feature
                {
                    Name = target.Table.Identifier,
                    Expression = new SelectExpression()
                    {
                        Columns = target.Columns,
                        From = new FromClause()
                        {
                            Expression = target.Table
                        }
                    }
                };
            }
            else
            {
                table = new TemporaryTableExpression()
                {
                    Name = target.Table.Identifier,
                    Expression = new SelectExpression()
                    {
                        Columns = target.Columns,
                        From = new FromClause()
                        {
                            Expression = target.Table
                        }
                    }
                };
            }

            _scope?.Children.Add(new ScriptScope(table, _scope));
        }
    }
}