using DaJet.Model;
using Microsoft.AspNetCore.Components;
using System.Diagnostics;

namespace DaJet.Studio.Pages.Flow
{
    public partial class PipelinePage : ComponentBase
    {
        private TreeNodeRecord _folder;
        private TreeNodeRecord _record;
        [Parameter] public Guid Uuid { get; set; }
        private string TreeNodeName { get; set; }
        private string PipelineName { get; set; }
        private IEnumerable<ProcessorRecord> Processors { get; set; } = new List<ProcessorRecord>();
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override async Task OnParametersSetAsync()
        {
            _record = await DataSource.SelectAsync<TreeNodeRecord>(Uuid);

            PipelineRecord pipeline = null;

            if (_record is not null)
            {
                pipeline = await DataSource.SelectAsync<PipelineRecord>(_record.Value);

                if (pipeline is not null)
                {
                    PipelineName = pipeline.Name;

                    _settings = await DataSource.QueryAsync<OptionRecord>(pipeline.GetEntity());
                }
                else
                {
                    PipelineName = "Pipeline is not found";
                }

                _folder = await DataSource.SelectAsync<TreeNodeRecord>(_record.Parent);

                if (_folder is not null)
                {
                    TreeNodeName = await DataSource.GetTreeNodeFullName(_folder);
                    TreeNodeName += "/" + _record.Name;
                }
                else
                {
                    TreeNodeName = "Tree node is not found";
                }
            }

            Processors = await DataSource.QueryAsync<ProcessorRecord>(pipeline.GetEntity());

            foreach (ProcessorRecord processor in Processors)
            {
                var options = await DataSource.QueryAsync<OptionRecord>(processor.GetEntity());

                if (options is not null)
                {
                    _options.Add(processor, options);
                }
            }
        }

        private IEnumerable<OptionRecord> _settings = new List<OptionRecord>();
        private IEnumerable<OptionRecord> GetPipelineOptions()
        {
            return _settings;
        }
        
        private readonly Dictionary<ProcessorRecord, IEnumerable<OptionRecord>> _options = new();
        private IEnumerable<OptionRecord> GetProcessorOptions(ProcessorRecord processor)
        {
            if (_options.TryGetValue(processor, out IEnumerable<OptionRecord> options))
            {
                return options;
            }
            return null;
        }
    }
}