using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Components.DataTable
{
    public partial class DataTable<TDataRow> : ComponentBase
    {
        [Parameter(CaptureUnmatchedValues = true)]
        public Dictionary<string, object> TableAttributes { get; set; }

        [Parameter]
        public ICollection<TDataRow> DataRows { get; set; }

        [Parameter]
        public RenderFragment ChildContent { get; set; } // DataColumn list fragment

        [Parameter]
        public Func<TDataRow, int, string> RowClass { get; set; }

        private readonly List<DataColumn<TDataRow>> DataColumns = new();

        // DataColumn uses this method to add itself to the columns collection
        internal void AddColumn(DataColumn<TDataRow> column)
        {
            DataColumns.Add(column);
        }
        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                // The first render will instantiate the DataColumn defined in the ChildContent.
                // Calling StateHasChanged() will re-render the component, so the second time it will know the columns
                StateHasChanged();
            }
        }
    }
}