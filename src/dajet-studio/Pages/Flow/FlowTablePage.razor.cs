using DaJet.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Flow
{
    public partial class FlowTablePage : ComponentBase
    {
        private TreeNodeRecord _folder;
        [Parameter] public Guid Uuid { get; set; }
        private string TreeNodeName { get; set; }
        private IEnumerable<PipelineState> Pipelines { get; set; } = new List<PipelineState>();
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override async Task OnInitializedAsync()
        {
            int typeCode = DomainModel.GetTypeCode(typeof(TreeNodeRecord));

            if (Uuid == Guid.Empty)
            {
                //TODO: show all pipelines
            }
            else
            {
                _folder = await DataSource.SelectAsync(new Entity(typeCode, Uuid)) as TreeNodeRecord;

                if (_folder is not null)
                {
                    TreeNodeName = await DataSource.GetTreeNodeFullName(_folder);
                }
            }

            await RefreshPipelineList();
        }
        private async Task RefreshPipelineList()
        {
            Dictionary<string, object> parameters = new()
            {
                { "TreeNode", _folder.GetEntity() }
            };
            
            Pipelines = await DataSource.SelectAsync<PipelineState>(parameters);
        }
    }
}