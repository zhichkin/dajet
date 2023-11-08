using DaJet.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Flow
{
    public partial class ProcessorSelectPage : ComponentBase
    {
        private TreeNodeRecord _folder;
        private PipelineRecord _record;
        [Parameter] public Guid Uuid { get; set; }
        private string TreeNodeName { get; set; }
        private string PipelineName { get; set; }
        private List<PipelineBlock> PipelineBlocks { get; set; } = new();
        private void NavigateToPipelinePage()
        {
            Navigator.NavigateTo($"/flow/pipeline/{_folder.Identity}");
        }
        protected override async Task OnParametersSetAsync()
        {
            _folder = await DataSource.SelectAsync<TreeNodeRecord>(Uuid);

            if (_folder is null)
            {
                TreeNodeName = "Tree node is not found";
            }
            else
            {
                TreeNodeName = await DataSource.GetTreeNodeFullName(_folder);

                _record = await DataSource.SelectAsync<PipelineRecord>(_folder.Value);

                if (_record is not null)
                {
                    PipelineName = _record.Name;
                }
                else
                {
                    PipelineName = "Pipeline is not found";
                }
            }

            PipelineBlocks = await DataSource.GetAvailableProcessors();
        }
        private async Task SelectProcessorForPipeline(PipelineBlock info)
        {
            int ordinal = 0;
            Entity pipeline = _record.GetEntity();
            
            var list = await DataSource.QueryAsync<HandlerRecord>(pipeline);

            if (list is List<HandlerRecord> processors)
            {
                foreach (var item in processors)
                {
                    if (item.Handler == info.Handler)
                    {
                        return; //TODO: duplicate handler error ?
                    }
                }

                ordinal = processors.Count;
            }

            HandlerRecord processor = DomainModel.New<HandlerRecord>();

            processor.Pipeline = pipeline;
            processor.Ordinal = ordinal;
            processor.Handler = info.Handler;
            processor.Message = info.Message;

            try
            {
                await DataSource.CreateAsync(processor);

                Entity owner = processor.GetEntity();

                foreach (OptionItem option in info.Options)
                {
                    OptionRecord record = DomainModel.New<OptionRecord>();

                    record.Owner = owner;
                    record.Name = option.Name;
                    record.Type = option.Type;
                    record.Value = option.Value;

                    if (record.Name == "Pipeline" && record.Type == "System.Guid" && string.IsNullOrWhiteSpace(record.Value))
                    {
                        record.Value = pipeline.Identity.ToString().ToLowerInvariant();
                    }

                    await DataSource.CreateAsync(record);
                }

                NavigateToPipelinePage();
            }
            catch
            {
                throw;
            }
        }
    }
}