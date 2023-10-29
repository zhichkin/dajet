using DaJet.Flow.Model;
using DaJet.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace DaJet.Studio.Pages.Flow
{
    public partial class PipelinePage : ComponentBase, IDisposable
    {
        private TreeNodeRecord _folder;
        private TreeNodeRecord _record;
        private System.Timers.Timer _timer;
        [Parameter] public Guid Uuid { get; set; }
        private string TreeNodeName { get; set; }
        private PipelineInfo PipeInfo { get; set; }
        private PipelineViewModel Pipeline { get; set; } = new();
        private void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override void OnInitialized()
        {
            _timer = new(TimeSpan.FromSeconds(5));
            _timer.Elapsed += TimerHandler;
            _timer.AutoReset = true;
        }
        public void Dispose()
        {
            _timer?.Dispose();
        }
        private void StartMonitor()
        {
            _timer?.Start();
        }
        private void StopMonitor()
        {
            _timer?.Stop();
        }
        private async void TimerHandler(object sender, System.Timers.ElapsedEventArgs args)
        {
            try
            {
                PipeInfo = await DataSource.GetPipelineInfo(Pipeline.Model.Identity);
                
                if (Pipeline is not null)
                {
                    Pipeline.Status = PipeInfo?.Status;
                }
            }
            catch
            {
                // show error to user
            }
            StateHasChanged();
        }
        private void RefreshPipelineServerInfo()
        {
            if (_timer.Enabled)
            {
                StopMonitor();
            }
            else
            {
                StartMonitor();
            }
        }
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
        private async Task MoveUp(ProcessorViewModel processor)
        {
            int count = Pipeline.Processors.Count;

            if (count == 0) { return; }

            int ordinal = processor.Model.Ordinal;

            if (ordinal == 0) { return; }

            int preceding = ordinal - 1;

            ProcessorViewModel moveup = null;
            ProcessorViewModel movedown = null;

            for (int i = 0; i < count; i++)
            {
                ProcessorViewModel current = Pipeline.Processors[i];

                if (current.Model.Ordinal == ordinal)
                {
                    ordinal = i;
                    moveup = current;
                    break;
                }
                else if (current.Model.Ordinal == preceding)
                {
                    preceding = i;
                    movedown = current;
                }
            }

            if (moveup is not null && movedown is not null)
            {
                Pipeline.Processors[ordinal] = movedown;
                Pipeline.Processors[preceding] = moveup;

                moveup.Model.Ordinal -= 1;
                movedown.Model.Ordinal += 1;

                await DataSource.UpdateAsync(moveup.Model);
                await DataSource.UpdateAsync(movedown.Model);
            }
        }
        private async Task MoveDown(ProcessorViewModel processor)
        {
            int count = Pipeline.Processors.Count;

            if (count == 0) { return; }

            int ordinal = processor.Model.Ordinal;

            if (ordinal >= count - 1) { return; }

            int following = ordinal + 1;

            ProcessorViewModel moveup = null;
            ProcessorViewModel movedown = null;

            for (int i = 0; i < count; i++)
            {
                ProcessorViewModel current = Pipeline.Processors[i];

                if (current.Model.Ordinal == ordinal)
                {
                    ordinal = i;
                    movedown = current;
                }
                else if (current.Model.Ordinal == following)
                {
                    following = i;
                    moveup = current;
                    break;
                }
            }

            if (moveup is not null && movedown is not null)
            {
                Pipeline.Processors[ordinal] = moveup;
                Pipeline.Processors[following] = movedown;

                moveup.Model.Ordinal -= 1;
                movedown.Model.Ordinal += 1;

                await DataSource.UpdateAsync(moveup.Model);
                await DataSource.UpdateAsync(movedown.Model);
            }
        }
        private async Task Remove(ProcessorViewModel processor)
        {
            int count = Pipeline.Processors.Count;

            if (count == 0) { return; }

            int ordinal = processor.Model.Ordinal;

            List<ProcessorViewModel> moveup = new();

            for (int i = 0; i < count; i++)
            {
                ProcessorViewModel current = Pipeline.Processors[i];

                if (current.Model.Ordinal > ordinal)
                {
                    moveup.Add(current);
                }
            }

            Pipeline.Processors.Remove(processor);
            await DataSource.DeleteAsync(processor.Model.GetEntity());

            foreach (ProcessorViewModel item in moveup)
            {
                item.Model.Ordinal -= 1;
                await DataSource.UpdateAsync(item.Model);
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
        private async Task ValidatePipeline()
        {
            try
            {
                Pipeline.IsValid = await DataSource.ValidatePipeline(Pipeline.Model.Identity);
            }
            catch (Exception error)
            {
                Pipeline.IsValid = false;
                Pipeline.Status = ExceptionHelper.GetErrorMessageAndStackTrace(error);
            }
        }
        private async Task ExecutePipeline()
        {
            await DataSource.ExecutePipeline(Pipeline.Model.Identity);
        }
        private async Task DisposePipeline()
        {
            await DataSource.DisposePipeline(Pipeline.Model.Identity);
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
        internal bool? IsValid { get; set; }
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
        internal string Status { get; set; }
        internal bool ShowPipelineStatus { get; set; }
        internal void TogglePipelineStatus()
        {
            ShowPipelineStatus = !ShowPipelineStatus;
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