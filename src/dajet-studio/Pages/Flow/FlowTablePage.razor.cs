using DaJet.Flow.Model;
using DaJet.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Flow
{
    public partial class FlowTablePage : ComponentBase
    {
        private TreeNodeRecord _folder;
        [Parameter] public Guid Uuid { get; set; }
        private string TreeNodeName { get; set; }
        private List<PipelineInfo> Pipelines { get; set; } = new();
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

                if (_folder is null)
                {
                    //TODO: handle error if Model is not found by uuid
                }

                TreeNodeName = await GetTreeNodeFullName(_folder);
            }

            await RefreshPipelineList();
        }
        private async Task<string> GetTreeNodeFullName(TreeNodeRecord node)
        {
            string name = node.Name;

            Entity parent = node.Parent;

            while (!parent.IsEmpty)
            {
                TreeNodeRecord record = await DataSource.SelectAsync(parent) as TreeNodeRecord;

                if (record is not null)
                {
                    name = record.Name + "/" + name;
                }
                else
                {
                    break;
                }

                parent = record.Parent;
            }

            return name;
        }
        private async Task RefreshPipelineList()
        {
            Pipelines = await DataSource.GetPipelineInfo();
        }
        private async Task ExecuteLinqExpression()
        {
            Guid uuid = Guid.NewGuid();

            IEnumerable<PipelineRecord> list = await DataSource
                .SelectAsync<PipelineRecord>(r => r.Identity == uuid);
        }
    }
}