using DaJet.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Flow
{
    public partial class PipelinePage : ComponentBase
    {
        private TreeNodeRecord _folder;
        private TreeNodeRecord _record;
        [Parameter] public Guid Uuid { get; set; }
        public PipelinePage()
        {
            _notifyOptionChanged = NotifyOptionChanged;
        }
        private string TreeNodeName { get; set; }
        private string PipelineName { get; set; }
        private List<ProcessorViewModel> Processors { get; set; } = new();
        private bool HasChanges { get; set; }
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

            var processors = await DataSource.QueryAsync<ProcessorRecord>(pipeline.GetEntity());

            Processors.Clear();

            foreach (ProcessorRecord processor in processors)
            {
                ProcessorViewModel viewModel = new(processor);

                Processors.Add(viewModel);

                var options = await DataSource.QueryAsync<OptionRecord>(processor.GetEntity());

                if (options is not null)
                {
                    foreach (var option in options)
                    {
                        viewModel.Options.Add(new OptionViewModel(option, _notifyOptionChanged));
                    }
                }
            }
        }

        private IEnumerable<OptionRecord> _settings = new List<OptionRecord>();
        private IEnumerable<OptionRecord> GetPipelineOptions()
        {
            return _settings;
        }

        private readonly Action _notifyOptionChanged;
        private void NotifyOptionChanged()
        {
            HasChanges = true;
        }
        private async Task SaveChanges()
        {

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
        private readonly Action _notifyChanged;
        internal OptionViewModel(OptionRecord model, Action notifyChanged)
        {
            _model = model;
            _notifyChanged = notifyChanged;
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
                    _notifyChanged();
                }
            }
        }
    }
}