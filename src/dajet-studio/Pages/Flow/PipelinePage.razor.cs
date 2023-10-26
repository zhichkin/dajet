using DaJet.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace DaJet.Studio.Pages.Flow
{
    public partial class PipelinePage : ComponentBase
    {
        private TreeNodeRecord _folder;
        private TreeNodeRecord _record;
        [Parameter] public Guid Uuid { get; set; }
        private string TreeNodeName { get; set; }
        private PipelineViewModel Pipeline { get; set; } = new();
        private void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override async Task OnParametersSetAsync()
        {
            _record = await DataSource.SelectAsync<TreeNodeRecord>(Uuid);

            if (_record is null) { return; }

            _folder = await DataSource.SelectAsync<TreeNodeRecord>(_record.Parent);

            if (_folder is null)
            {
                TreeNodeName = "нет";
            }
            else
            {
                TreeNodeName = await DataSource.GetTreeNodeFullName(_folder);
            }

            PipelineRecord pipeline = await DataSource.SelectAsync<PipelineRecord>(_record.Value);

            if (pipeline is null) { return; }

            Pipeline.SetDataContext(pipeline);

            var settings = await DataSource.QueryAsync<OptionRecord>(pipeline.GetEntity());

            if (settings is not null)
            {
                Pipeline.Options.Clear();

                foreach (OptionRecord setting in settings)
                {
                    Pipeline.Options.Add(new OptionViewModel(setting));
                }
            }

            var processors = await DataSource.QueryAsync<ProcessorRecord>(pipeline.GetEntity());

            if (processors is not null)
            {
                Pipeline.Processors.Clear();

                foreach (ProcessorRecord processor in processors)
                {
                    ProcessorViewModel viewModel = new(processor);

                    Pipeline.Processors.Add(viewModel);

                    var options = await DataSource.QueryAsync<OptionRecord>(processor.GetEntity());

                    if (options is not null)
                    {
                        foreach (OptionRecord option in options)
                        {
                            viewModel.Options.Add(new OptionViewModel(option));
                        }
                    }
                }
            }
        }
        private async Task SaveChanges()
        {
            Pipeline.HasChanges = false;
        }
        private void SelectProcessor()
        {
            Navigator.NavigateTo($"/flow/processor/select/{_record.Identity}");
        }
        private async Task OnBeforeInternalNavigation(LocationChangingContext context)
        {
            if (Pipeline is not null && Pipeline.HasChanges)
            {
                string message = "Есть не сохранённые данные. Продолжить ?";

                bool confirmed = await JSRuntime.InvokeAsync<bool>("confirm", message);

                if (!confirmed)
                {
                    context.PreventNavigation();
                }
            }
        }
    }
    internal sealed class PipelineViewModel
    {
        private PipelineRecord _model;
        internal void SetDataContext(PipelineRecord pipeline)
        {
            _model = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }
        internal string Name
        {
            get { return _model?.Name; }
            set
            {
                _model.Name = value;

                if (!_model.IsOriginal())
                {
                    HasChanges = true;
                }
            }
        }
        internal string Activation
        {
            get { return _model?.Activation.ToString(); }
            set
            {
                _model.Activation = Enum.Parse<PipelineMode>(value);

                if (!_model.IsOriginal())
                {
                    HasChanges = true;
                }
            }
        }
        internal List<OptionViewModel> Options { get; } = new();
        internal List<ProcessorViewModel> Processors { get; } = new();
        internal bool HasChanges { get; set; }
        internal bool ShowOptions { get; set; }
        internal void ToggleOptions()
        {
            ShowOptions = !ShowOptions;
        }
    }
    internal sealed class ProcessorViewModel
    {
        private readonly ProcessorRecord _model;
        internal ProcessorViewModel(ProcessorRecord model)
        {
            _model = model;
        }
        internal string Handler { get { return _model.Handler; } }
        internal List<OptionViewModel> Options { get; } = new();
        internal bool ShowOptions { get; set; }
        internal void ToggleOptions()
        {
            ShowOptions = !ShowOptions;
        }
    }
    internal sealed class OptionViewModel
    {
        private readonly OptionRecord _model;
        internal OptionViewModel(OptionRecord model)
        {
            _model = model;
        }
        internal string Name { get { return _model.Name; } }
        internal string Value
        {
            get { return _model.Value; }
            set
            {
                _model.Value = value;

                if (!_model.IsOriginal())
                {
                    
                }
            }
        }
    }
}