using Microsoft.AspNetCore.Components;
using System.Linq.Expressions;

namespace DaJet.Studio.Components.DataTable
{
    public partial class DataColumn<TDataRow> : ComponentBase
    {
        [CascadingParameter]
        public DataTable<TDataRow> OwnerTable { get; set; }

        [Parameter]
        public string Title { get; set; }

        [Parameter]
        public Expression<Func<TDataRow, object>> Expression { get; set; }

        [Parameter]
        public string Format { get; set; }

        [Parameter]
        public RenderFragment<TDataRow> ChildContent { get; set; }

        private Func<TDataRow, object> compiledExpression;
        private Expression lastCompiledExpression;
        private RenderFragment headerTemplate;
        private RenderFragment<TDataRow> cellTemplate;

        protected override void OnInitialized()
        {
            OwnerTable.AddColumn(this);
        }
        protected override void OnParametersSet()
        {
            if (lastCompiledExpression != Expression)
            {
                compiledExpression = Expression?.Compile();
                lastCompiledExpression = Expression;
            }
        }
        internal RenderFragment HeaderTemplate
        {
            get
            {
                return headerTemplate ??= (builder =>
                {
                    string title = Title;
                    
                    if (title is null && Expression is not null)
                    {
                        title = GetMemberName(Expression);
                    }

                    builder.OpenElement(0, "th");
                    builder.AddContent(1, title);
                    builder.CloseElement();
                });
            }
        }
        internal RenderFragment<TDataRow> CellTemplate
        {
            get
            {
                return cellTemplate ??= (rowData => builder =>
                {
                    builder.OpenElement(0, "td");
                    if (compiledExpression != null)
                    {
                        var value = compiledExpression(rowData);
                        var formattedValue = string.IsNullOrEmpty(Format) ? value?.ToString() : string.Format("{0:" + Format + "}", value);
                        builder.AddContent(1, formattedValue);
                    }
                    else
                    {
                        builder.AddContent(2, ChildContent, rowData);
                    }

                    builder.CloseElement();
                });
            }
        }
        private static string GetMemberName<T>(Expression<T> expression)
        {
            return expression.Body switch
            {
                MemberExpression m => m.Member.Name,
                UnaryExpression u when u.Operand is MemberExpression m => m.Member.Name,
                _ => throw new NotSupportedException("Expression of type '" + expression.GetType().ToString() + "' is not supported")
            };
        }
    }
}