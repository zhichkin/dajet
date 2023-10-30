using DaJet.Flow.Model;
using DaJet.Model;
using Microsoft.AspNetCore.Components;
using System.Xml.Linq;

namespace DaJet.Studio.Pages.Flow
{
    public partial class FlowTablePage : ComponentBase
    {
        private TreeNodeRecord _folder;
        [Parameter] public Guid Uuid { get; set; }
        private string TreeNodeName { get; set; }
        private List<PipelineInfo> Pipelines { get; set; } = new();
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override async Task OnParametersSetAsync()
        {
            await RefreshPipelineList();
        }
        private async Task RefreshPipelineList()
        {
            Pipelines.Clear();

            if (Uuid == Guid.Empty)
            {
                TreeNodeName = "/flow";
                Pipelines = await DataSource.GetPipelineInfo();
                return;
            }

            _folder = await DataSource.SelectAsync<TreeNodeRecord>(Uuid);

            if (_folder is null)
            {
                TreeNodeName = "ERROR: tree node is not found !!!"; return;
            }

            TreeNodeName = await DataSource.GetTreeNodeFullName(_folder);

            var nodes = await DataSource.QueryAsync<TreeNodeRecord>(_folder.GetEntity());

            foreach (var node in nodes)
            {
                if (node.IsFolder) { continue; }

                PipelineInfo info = await DataSource.GetPipelineInfo(node.Value.Identity);

                Pipelines.Add(info);
            }
        }
        private async Task ExecutePipeline(PipelineInfo pipeline)
        {
            await DataSource.ExecutePipeline(pipeline.Uuid);
            await RefreshPipelineInfo(pipeline);
        }
        private async Task DisposePipeline(PipelineInfo pipeline)
        {
            await DataSource.DisposePipeline(pipeline.Uuid);
            await RefreshPipelineInfo(pipeline);
        }
        private async Task RefreshPipelineInfo(PipelineInfo pipeline)
        {
            PipelineInfo info = await DataSource.GetPipelineInfo(pipeline.Uuid);

            pipeline.State = info.State;
            pipeline.Start = info.Start;
            pipeline.Finish = info.Finish;
            pipeline.Status = info.Status;
        }
        private async Task NavigateToPipelinePage(PipelineInfo pipeline)
        {
            var nodes = await DataSource.QueryAsync<TreeNodeRecord>(_folder.GetEntity());

            TreeNodeRecord record = null;

            foreach (var node in nodes)
            {
                if (node.Value.Identity == pipeline.Uuid)
                {
                    record = node; break;
                }
            }

            if (record is not null)
            {
                Navigator.NavigateTo($"/flow/pipeline/{record.Identity.ToString().ToLower()}");
            }
        }
    }
}