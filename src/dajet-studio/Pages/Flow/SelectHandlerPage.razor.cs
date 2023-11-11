using DaJet.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Flow
{
    public partial class SelectHandlerPage : ComponentBase
    {
        private TreeNodeRecord _folder;
        private PipelineRecord _record;
        [Parameter] public Guid Uuid { get; set; }
        private string TreeNodeName { get; set; }
        private string PipelineName { get; set; }
        private List<HandlerModel> Handlers { get; set; } = new();
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

            Handlers = await DataSource.GetAvailableHandlers();
        }
        private async Task SelectHandlerForPipeline(HandlerModel model)
        {
            int ordinal = 0;
            Entity pipeline = _record.GetEntity();
            
            var list = await DataSource.QueryAsync<HandlerRecord>(pipeline);

            if (list is List<HandlerRecord> handlers)
            {
                foreach (var item in handlers)
                {
                    if (item.Handler == model.Handler)
                    {
                        return; //TODO: duplicate handler error ?
                    }
                }

                ordinal = handlers.Count;
            }

            HandlerRecord new_handler = DomainModel.New<HandlerRecord>();

            new_handler.Pipeline = pipeline;
            new_handler.Ordinal = ordinal;
            new_handler.Handler = model.Handler;
            new_handler.Message = model.Message;

            try
            {
                await DataSource.CreateAsync(new_handler);

                Entity owner = new_handler.GetEntity();

                foreach (OptionModel option in model.Options)
                {
                    OptionRecord new_option = DomainModel.New<OptionRecord>();

                    new_option.Owner = owner;
                    new_option.Name = option.Name;
                    new_option.Type = option.Type;
                    new_option.Value = option.Value;

                    if (new_option.Name == "Pipeline" && new_option.Type == "System.Guid" && string.IsNullOrWhiteSpace(new_option.Value))
                    {
                        new_option.Value = pipeline.Identity.ToString().ToLowerInvariant();
                    }

                    await DataSource.CreateAsync(new_option);
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