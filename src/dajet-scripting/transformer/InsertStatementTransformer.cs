using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class InsertStatementTransformer : IScriptTransformer
    {
        public void Transform(in SyntaxNode node)
        {
            if (node is not InsertStatement insert) { return; }

            object source = DataMapper.GetColumnSource(insert.Source);
            
            if (source is not SelectExpression select) { return; }

            ColumnExpression vectorColumn = GetVectorColumn(in select);

            if (vectorColumn is null) { return; }

            string sourceName = DataMapper.GetColumnSourceName(insert.Source);
            
            insert.Source = TransformColumnSource(in select, in sourceName);

            select.Select.Remove(vectorColumn);
        }
        public string GetVectorColumnName(in InsertStatement insert)
        {
            object source = DataMapper.GetColumnSource(insert.Source);

            if (source is not SelectExpression select) { return null; }

            ColumnExpression vectorColumn = GetVectorColumn(in select);

            if (vectorColumn is null) { return null; }

            ColumnExpression column;
            List<ColumnExpression> columns = select.Select;

            for (int i = 0; i < columns.Count; i++)
            {
                column = columns[i];

                if (column.Expression is FunctionExpression function)
                {
                    if (function.Name.ToUpper() == "VECTOR")
                    {
                        return column.Alias;
                    }
                }
            }

            return null;
        }
        private ColumnExpression GetVectorColumn(in SelectExpression select)
        {
            ColumnExpression column;
            List<ColumnExpression> columns = select.Select;

            for (int i = 0; i < columns.Count; i++)
            {
                column = columns[i];

                if (column.Expression is FunctionExpression function)
                {
                    if (function.Name.ToUpper() == "VECTOR")
                    {
                        return column;
                    }
                }
            }

            return null;
        }
        private TableReference TransformColumnSource(in SelectExpression select, in string sourceName)
        {
            SelectExpression columnSource = new();

            TableReference table = new()
            {
                Identifier = sourceName,
                Binding = new CommonTableExpression()
                {
                    Name = sourceName,
                    Expression = columnSource
                }
            };

            ColumnExpression column;
            List<ColumnExpression> columns = select.Select;

            for (int i = 0; i < columns.Count; i++)
            {
                column = columns[i];

                columnSource.Select.Add(column);

                //if (column.Expression is FunctionExpression function)
                //{
                //    if (function.Name.ToUpper() == "VECTOR")
                //    {
                //        return column;
                //    }
                //}
            }

            return table;
        }
    }
}