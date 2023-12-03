using DaJet.Data;
using DaJet.Model;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace DaJet.Studio.Pages.Flow
{
    public partial class FlowTablePage : ComponentBase
    {
        private string _filter;
        private TreeNodeRecord _folder;
        [Parameter] public Guid Uuid { get; set; }
        private string TreeNodeName { get; set; }
        private string FilterValue
        {
            get { return _filter; }
            set { _filter = value; FilterPipelineTable(); }
        }
        private List<PipelineInfoViewModel> Pipelines { get; set; } = new();
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
                List<PipelineInfo> list = await DataSource.GetPipelineInfo();
                foreach(PipelineInfo info in list)
                {
                    Pipelines.Add(new PipelineInfoViewModel(info));
                }
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

                Pipelines.Add(new PipelineInfoViewModel(info));
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
            int typeCode = DomainModel.GetTypeCode(typeof(PipelineRecord));

            string query = "SELECT uuid FROM maintree WHERE entity_type = @code AND entity_uuid = @uuid;";

            Dictionary<string, object> parameters = new()
            {
                { "code", typeCode },
                { "uuid", pipeline.Uuid }
            };

            List<DataObject> list = await DataSource.QueryAsync(query, parameters);

            if (list is null || list.Count == 0) { return; }

            Guid identity = list[0].GetGuid(0);

            TreeNodeRecord record = await DataSource.SelectAsync<TreeNodeRecord>(identity);

            if (record is not null)
            {
                Navigator.NavigateTo($"/flow/pipeline/{record.Identity.ToString().ToLower()}");
            }
        }
        private void FilterPipelineTable()
        {
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo("en-US");
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.CurrentUICulture;
            }

            if (string.IsNullOrWhiteSpace(_filter))
            {
                ClearPipelineTableFilter();
            }
            else
            {
                ApplyPipelineTableFilter(in culture, in _filter);
            }
        }
        private void ClearPipelineTableFilter()
        {
            _filter = string.Empty;

            foreach (PipelineInfoViewModel model in Pipelines)
            {
                model.IsVisible = true;
            }
        }
        private void ApplyPipelineTableFilter(in CultureInfo culture, in string filter)
        {
            foreach (PipelineInfoViewModel info in Pipelines)
            {
                info.IsVisible = culture.CompareInfo.IndexOf(info.Model.Name, filter, CompareOptions.IgnoreCase) > -1;
            }
        }
    }
    internal class PipelineInfoViewModel
    {
        private readonly PipelineInfo _model;
        internal PipelineInfoViewModel(PipelineInfo pipeline)
        {
            _model = pipeline;
        }
        internal PipelineInfo Model { get { return _model; } }
        internal bool IsVisible { get; set; } = true;
    }
}