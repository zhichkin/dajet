using DaJet.Metadata.Model;
using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class UpdateStatementTransformer : IScriptWalker, IScriptTransformer
    {
        private readonly HashSet<ColumnExpression> _columns = new();
        public void Transform(in SyntaxNode node)
        {
            if (node is not UpdateStatement update) { return; }

            // transformation is only allowed when targeting temporary tables !!!

            if (update.Target.Binding is not TableVariableExpression &&
                update.Target.Binding is not TemporaryTableExpression)
            {
                return;
            }

            // only derived tables and CTE are allowed to be transformed !!!

            if (update.Source is not TableExpression)
            {
                if (update.Source is TableReference table && table.Binding is not CommonTableExpression)
                {
                    return;
                }
            }

            object source = DataMapper.GetColumnSource(update.Source);

            if (source is SelectExpression select)
            {
                Transform(in select);
            }
            else if (source is ApplicationObject entity)
            {
                return; //TODO: transform UPDATE database object source
            }

            ScriptWalker.Walk(in node, this);
        }
        private void Transform(in SelectExpression source)
        {
            _columns.Clear();

            foreach (ColumnExpression column in source.Select)
            {
                column.Alias = $"_{column.Alias}";

                _columns.Add(column);
            }
        }
        public void SayHello(SyntaxNode node)
        {
            return; // not implemented
        }
        public void SayGoodbye(SyntaxNode node)
        {
            if (node is not null) { TransformInternal(in node); }
        }
        private void TransformInternal(in SyntaxNode node)
        {
            if (node is not ColumnReference column ||
                column.Binding is not ColumnExpression parent ||
                !_columns.Contains(parent) || column.Mapping is null)
            {
                return;
            }

            foreach (ColumnMap map in column.Mapping)
            {
                ScriptHelper.GetColumnIdentifiers(map.Name, out string tableAlias, out string columnName);

                if (string.IsNullOrEmpty(tableAlias))
                {
                    map.Name = $"_{columnName}";
                }
                else
                {
                    map.Name = $"{tableAlias}._{columnName}";
                }
            }
        }
    }
}