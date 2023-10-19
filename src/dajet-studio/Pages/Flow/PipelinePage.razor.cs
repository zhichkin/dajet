using DaJet.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Flow
{
    public partial class PipelinePage : ComponentBase
    {
        private TreeNodeRecord _folder;
        private TreeNodeRecord _record;
        [Parameter] public Guid Uuid { get; set; }
        private string TreeNodeName { get; set; }
        private string PipelineName { get; set; }
        private IEnumerable<PipelineRecord> Pipelines { get; set; } = new List<PipelineRecord>();
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override async Task OnParametersSetAsync()
        {
            int typeCode = DomainModel.GetTypeCode(typeof(TreeNodeRecord));
            
            _record = await DataSource.SelectAsync(new Entity(typeCode, Uuid)) as TreeNodeRecord;

            if (_record is not null)
            {
                PipelineRecord pipeline = await DataSource.SelectAsync(_record.Value) as PipelineRecord;

                if (pipeline is not null)
                {
                    PipelineName = pipeline.Name;
                }

                _folder = await DataSource.SelectAsync(_record.Parent) as TreeNodeRecord;

                if (_folder is not null)
                {
                    TreeNodeName = await DataSource.GetTreeNodeFullName(_folder);
                }
            }

            Pipelines = await DataSource.SelectAsync<PipelineRecord>();

            foreach (PipelineRecord record in Pipelines)
            {
                Console.WriteLine(record.Name);

                var list = await DataSource.SelectAsync<ProcessorRecord>(record.GetEntity());

                foreach (var item in list)
                {
                    Console.WriteLine("- " + item.Handler);

                    var options = await DataSource.SelectAsync<OptionRecord>(item.GetEntity());

                    foreach (var option in options)
                    {
                        Console.WriteLine("  - " + option.Name + " = " + option.Value);
                    }
                }
            }
        }
    }
}