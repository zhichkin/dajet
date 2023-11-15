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
        private void NavigateToPipelinePage() { Navigator.NavigateTo($"/flow/pipeline/{_folder.Identity}"); }
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
            Entity pipeline = _record.GetEntity();
            
            var handlers = await DataSource.QueryAsync<HandlerRecord>(pipeline);

            if (handlers is null) { return; }

            int ordinal = handlers.Count();

            foreach (var item in handlers)
            {
                if (item.Name == model.Name)
                {
                    return; //TODO: throw duplicate handler error ?
                }
            }

            try
            {
                HandlerRecord new_handler = DomainModel.New<HandlerRecord>();
                new_handler.Pipeline = pipeline;
                new_handler.Ordinal = ordinal;
                new_handler.Name = model.Name;
                await DataSource.CreateAsync(new_handler);

                List<OptionModel> options = await DataSource.GetAvailableOptions(new_handler.Name);

                if (options is not null && options.Count > 0)
                {
                    Entity owner = new_handler.GetEntity();

                    foreach (OptionModel option in options)
                    {
                        if (option.IsRequired)
                        {
                            OptionRecord new_option = DomainModel.New<OptionRecord>();
                            new_option.Owner = owner;
                            new_option.Name = option.Name;
                            new_option.Type = option.Type;
                            new_option.Value = option.Value;
                            await DataSource.CreateAsync(new_option);
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
            
            NavigateToPipelinePage();
        }
    }
}