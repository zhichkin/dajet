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
            _record = await DataSource.SelectAsync<TreeNodeRecord>(Uuid);

            if (_record is not null)
            {
                PipelineRecord pipeline = await DataSource.SelectAsync<PipelineRecord>(_record.Value);

                if (pipeline is not null)
                {
                    PipelineName = pipeline.Name;
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

            //Pipelines = await DataSource.QueryAsync<PipelineRecord>();

            //foreach (PipelineRecord record in Pipelines)
            //{
            //    Console.WriteLine(record.Name);

            //    var list = await DataSource.QueryAsync<ProcessorRecord>(record.GetEntity());

            //    foreach (var item in list)
            //    {
            //        Console.WriteLine("- " + item.Handler);

            //        var options = await DataSource.QueryAsync<OptionRecord>(item.GetEntity());

            //        foreach (var option in options)
            //        {
            //            Console.WriteLine("  - " + option.Name + " = " + option.Value);
            //        }
            //    }
            //}
        }
    }
}