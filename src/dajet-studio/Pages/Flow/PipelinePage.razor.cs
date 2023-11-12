using DaJet.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Options;
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
                TreeNodeName += "/" + _record.Name;
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

            var handlers = await DataSource.QueryAsync<HandlerRecord>(pipeline.GetEntity());

            if (handlers is not null)
            {
                Pipeline.Handlers.Clear();

                foreach (HandlerRecord handler in handlers)
                {
                    HandlerViewModel viewModel = Pipeline.Add(handler);

                    viewModel.AvailableOptions.Clear();
                    
                    List<OptionModel> availableOptions = await DataSource.GetAvailableOptions(handler.Handler);

                    if (availableOptions is not null && availableOptions.Count > 0)
                    {
                        foreach (OptionModel optionModel in availableOptions)
                        {
                            viewModel.AvailableOptions.Add(optionModel.Name, optionModel);
                        }
                    }

                    var options = await DataSource.QueryAsync<OptionRecord>(handler.GetEntity());

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
        private async Task MoveUp(HandlerViewModel handler)
        {
            int count = Pipeline.Handlers.Count;

            if (count == 0) { return; }

            int ordinal = handler.Model.Ordinal;

            if (ordinal == 0) { return; }

            int preceding = ordinal - 1;

            HandlerViewModel moveup = null;
            HandlerViewModel movedown = null;

            for (int i = 0; i < count; i++)
            {
                HandlerViewModel current = Pipeline.Handlers[i];

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
                Pipeline.Handlers[ordinal] = movedown;
                Pipeline.Handlers[preceding] = moveup;

                moveup.Model.Ordinal -= 1;
                movedown.Model.Ordinal += 1;

                await DataSource.UpdateAsync(moveup.Model);
                await DataSource.UpdateAsync(movedown.Model);
            }
        }
        private async Task MoveDown(HandlerViewModel handler)
        {
            int count = Pipeline.Handlers.Count;

            if (count == 0) { return; }

            int ordinal = handler.Model.Ordinal;

            if (ordinal >= count - 1) { return; }

            int following = ordinal + 1;

            HandlerViewModel moveup = null;
            HandlerViewModel movedown = null;

            for (int i = 0; i < count; i++)
            {
                HandlerViewModel current = Pipeline.Handlers[i];

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
                Pipeline.Handlers[ordinal] = moveup;
                Pipeline.Handlers[following] = movedown;

                moveup.Model.Ordinal -= 1;
                movedown.Model.Ordinal += 1;

                await DataSource.UpdateAsync(moveup.Model);
                await DataSource.UpdateAsync(movedown.Model);
            }
        }
        private async Task Remove(HandlerViewModel handler)
        {
            string message = $"Удалить блок \"{handler.Handler}\" ?";

            bool confirmed = await JSRuntime.InvokeAsync<bool>("confirm", message);

            if (!confirmed) { return; }

            int count = Pipeline.Handlers.Count;

            if (count == 0) { return; }

            int ordinal = handler.Model.Ordinal;

            List<HandlerViewModel> moveup = new();

            for (int i = 0; i < count; i++)
            {
                HandlerViewModel current = Pipeline.Handlers[i];

                if (current.Model.Ordinal > ordinal)
                {
                    moveup.Add(current);
                }
            }

            Pipeline.Handlers.Remove(handler);
            await DataSource.DeleteAsync(handler.Model.GetEntity());

            foreach (HandlerViewModel item in moveup)
            {
                item.Model.Ordinal -= 1;
                await DataSource.UpdateAsync(item.Model);
            }
        }
        private async Task SaveChanges()
        {
            foreach (HandlerViewModel handler in Pipeline.Handlers)
            {
                foreach (OptionViewModel option in handler.Options)
                {
                    if (option.Model.IsNew())
                    {
                        //await DataSource.CreateAsync(option.Model);

                        string message = $"\"{option.Model.Name}\" [{option.Model.Type}] = {option.Model.Value}";

                        await JSRuntime.InvokeVoidAsync("alert", message);
                    }
                    else if (option.Model.IsChanged())
                    {
                        await DataSource.UpdateAsync(option.Model);
                    }
                }

                if (handler.Model.IsChanged())
                {
                    await DataSource.UpdateAsync(handler.Model);
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
        private void SelectHandler()
        {
            Navigator.NavigateTo($"/flow/handler/select/{_record.Identity}");
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

                if (Pipeline.IsValid.HasValue && Pipeline.IsValid.Value)
                {
                    Pipeline.Status = string.Empty;
                }
            }
            catch (Exception error)
            {
                Pipeline.IsValid = false;
                Pipeline.Status = ExceptionHelper.GetErrorMessageAndStackTrace(error);
            }
        }
        private async Task ReStartPipeline()
        {
            try
            {
                await DataSource.ReStartPipeline(Pipeline.Model.Identity);
            }
            catch (Exception error)
            {
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

        private void AddHandlerOption(HandlerViewModel model)
        {
            OptionRecord record = DomainModel.New<OptionRecord>();

            record.Owner = model.Model.GetEntity();

            model.Insert(record);
        }
    }
    internal interface IChangeNotifier
    {
        void NotifyChange();
    }
    internal interface IOptionsOwner
    {
        List<OptionModel> GetAvailableOptions();
        OptionModel GetOptionByName(string name);
    }
    internal sealed class PipelineViewModel : IChangeNotifier, IOptionsOwner
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
                _model.Activation = Enum.Parse<ActivationMode>(value);

                if (!_model.IsOriginal())
                {
                    IsChanged = true;
                }
            }
        }
        internal void Add(OptionRecord record)
        {
            OptionViewModel option = new(this, record);
            option.SetChangeNotifier(this);
            Options.Add(option);
        }
        internal HandlerViewModel Add(HandlerRecord record)
        {
            HandlerViewModel handler = new(record);
            handler.SetChangeNotifier(this);
            Handlers.Add(handler);
            return handler;
        }
        internal List<OptionViewModel> Options { get; } = new();
        internal List<HandlerViewModel> Handlers { get; } = new();
        internal Dictionary<string, OptionModel> AvailableOptions { get; } = new();
        public List<OptionModel> GetAvailableOptions()
        {
            List<OptionModel> list = new();

            foreach (var item in AvailableOptions)
            {
                bool found = false;

                foreach (OptionViewModel view in Options)
                {
                    if (item.Key == view.Model.Name)
                    {
                        found = true; break;
                    }
                }

                if (!found)
                {
                    list.Add(item.Value);
                }
            }
            
            return list;
        }
        public OptionModel GetOptionByName(string name)
        {
            return AvailableOptions.TryGetValue(name, out OptionModel option) ? option : null;
        }
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
    internal sealed class HandlerViewModel : IChangeNotifier, IOptionsOwner
    {
        private IChangeNotifier _notifier;
        private readonly HandlerRecord _model;
        internal HandlerViewModel(HandlerRecord model)
        {
            _model = model;
        }
        internal HandlerRecord Model { get { return _model; } }
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
            OptionViewModel option = new(this, record);
            option.SetChangeNotifier(this);
            Options.Add(option);
        }
        internal void Insert(OptionRecord record)
        {
            OptionViewModel option = new(this, record);
            option.SetChangeNotifier(this);
            Options.Insert(0, option);
            _notifier?.NotifyChange();
        }

        internal string Handler { get { return _model.Handler; } }
        internal List<OptionViewModel> Options { get; } = new();
        internal Dictionary<string, OptionModel> AvailableOptions { get; } = new();
        public List<OptionModel> GetAvailableOptions()
        {
            List<OptionModel> list = new();

            foreach (var item in AvailableOptions)
            {
                bool found = false;

                foreach (OptionViewModel view in Options)
                {
                    if (item.Key == view.Model.Name)
                    {
                        found = true; break;
                    }
                }

                if (!found)
                {
                    list.Add(item.Value);
                }
            }

            return list;
        }
        public OptionModel GetOptionByName(string name)
        {
            return AvailableOptions.TryGetValue(name, out OptionModel option) ? option : null;
        }
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
        private readonly IOptionsOwner _owner;
        internal OptionViewModel(IOptionsOwner owner, OptionRecord model)
        {
            _owner = owner;
            _model = model;
        }
        internal OptionRecord Model { get { return _model; } }
        internal void SetChangeNotifier(IChangeNotifier notifier)
        {
            _notifier = notifier;
        }
        internal string Name
        {
            get { return _model.Name; }
        }
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
        internal void OptionSelected(ChangeEventArgs args)
        {
            if (args.Value is string optionName)
            {
                OptionModel option = _owner.GetOptionByName(optionName);

                if (option is not null)
                {
                    _model.Name = optionName;
                    _model.Type = option.Type;
                    _model.Value = option.Value;
                    _notifier?.NotifyChange();
                }
            }
        }
    }
}