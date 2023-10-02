using DaJet.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Flow
{
    public partial class FlowTablePage : ComponentBase
    {
        private TreeNodeRecord _folder;
        [Parameter] public Guid Uuid { get; set; }
        private List<PipelineRecord> Pipelines { get; set; } = new();

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
            }

            await RefreshPipelineList();
        }
        private async Task RefreshPipelineList()
        {
            //IsLoading = true;

            Pipelines.Clear();

            var list = await DataSource.SelectAsync<TreeNodeRecord>("parent", _folder.GetEntity());

            foreach (var item in list)
            {
                if (item is TreeNodeRecord record)
                {
                    Pipelines.Add(new PipelineRecord()
                    {
                        Name = record.Name
                    });
                }
            }

            //IsLoading = false;
        }
    }
}