using DaJet.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using System.Diagnostics;

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
                    Pipeline.Add(setting);
                }
            }

            var processors = await DataSource.QueryAsync<ProcessorRecord>(pipeline.GetEntity());

            if (processors is not null)
            {
                Pipeline.Processors.Clear();

                foreach (ProcessorRecord processor in processors)
                {
                    ProcessorViewModel viewModel = Pipeline.Add(processor);
                    
                    var options = await DataSource.QueryAsync<OptionRecord>(processor.GetEntity());

                    if (options is not null)
                    {
                        foreach (OptionRecord option in options)
                        {
                            viewModel.Add(option);
                        }
                    }
                }
            }
        }
        private async Task SaveChanges()
        {
            foreach (ProcessorViewModel processor in Pipeline.Processors)
            {
                foreach (OptionViewModel option in processor.Options)
                {
                    if (option.Model.IsChanged())
                    {
                        await DataSource.UpdateAsync(option.Model);
                    }
                }

                if (processor.Model.IsChanged())
                {
                    await DataSource.UpdateAsync(processor.Model);
                }
            }

            foreach (OptionViewModel option in Pipeline.Options)
            {
                if (option.Model.IsChanged())
                {
                    await DataSource.UpdateAsync(option.Model);
                }
            }

            if (Pipeline.Model.IsChanged())
            {
                await DataSource.UpdateAsync(Pipeline.Model);
            }

            Pipeline.IsChanged = false;
        }
        private void SelectProcessor()
        {
            Navigator.NavigateTo($"/flow/processor/select/{_record.Identity}");
        }
        private async Task OnBeforeInternalNavigation(LocationChangingContext context)
        {
            if (Pipeline is not null && Pipeline.IsChanged)
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
    internal interface IChangeNotifier
    {
        void NotifyChange();
    }
    internal sealed class PipelineViewModel : IChangeNotifier
    {
        private PipelineRecord _model;
        internal void SetDataContext(PipelineRecord pipeline)
        {
            _model = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }
        internal PipelineRecord Model { get { return _model; } }
        internal bool IsChanged { get; set; }
        public void NotifyChange() { IsChanged = true; }
        internal string Name
        {
            get { return _model?.Name; }
            set
            {
                _model.Name = value;

                if (!_model.IsOriginal())
                {
                    IsChanged= true;
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
                    IsChanged = true;
                }
            }
        }
        internal void Add(OptionRecord record)
        {
            OptionViewModel option = new(record);
            option.SetChangeNotifier(this);
            Options.Add(option);
        }
        internal ProcessorViewModel Add(ProcessorRecord record)
        {
            ProcessorViewModel processor = new(record);
            processor.SetChangeNotifier(this);
            Processors.Add(processor);
            return processor;
        }
        internal List<OptionViewModel> Options { get; } = new();
        internal List<ProcessorViewModel> Processors { get; } = new();
        internal bool ShowOptions { get; set; }
        internal void ToggleOptions()
        {
            ShowOptions = !ShowOptions;
        }
    }
    internal sealed class ProcessorViewModel : IChangeNotifier
    {
        private IChangeNotifier _notifier;
        private readonly ProcessorRecord _model;
        internal ProcessorViewModel(ProcessorRecord model)
        {
            _model = model;
        }
        internal ProcessorRecord Model { get { return _model; } }
        internal void SetChangeNotifier(IChangeNotifier notifier)
        {
            _notifier = notifier;
        }
        public void NotifyChange()
        {
            _notifier?.NotifyChange();
        }
        internal void Add(OptionRecord record)
        {
            OptionViewModel option = new(record);
            option.SetChangeNotifier(this);
            Options.Add(option);
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
        private IChangeNotifier _notifier;
        private readonly OptionRecord _model;
        internal OptionViewModel(OptionRecord model)
        {
            _model = model;
        }
        internal OptionRecord Model { get { return _model; } }
        internal void SetChangeNotifier(IChangeNotifier notifier)
        {
            _notifier = notifier;
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
                    _notifier?.NotifyChange();
                }
            }
        }
    }
}