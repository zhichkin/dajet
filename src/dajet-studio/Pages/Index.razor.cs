using DaJet.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DaJet.Studio.Pages
{
    public partial class Index : ComponentBase, IDisposable
    {
        protected override async Task OnInitializedAsync()
        {
            
        }
        public void Dispose()
        {
            
        }
        protected async Task Test(MouseEventArgs args)
        {
            ODataSource context = new(new Uri("https://localhost:5001/home"));
            //IEnumerable<TreeNodeRecord> records = await context.TreeNodes.ExecuteAsync();
            //IEnumerable<TreeNodeRecord> records = await context.TreeNodes.Expand("Value").ExecuteAsync();

            IEnumerable<TreeNodeRecord> records = await context.TreeNodes
                .AddQueryOption("$filter", "Parent/Name eq 'root'")
                // .Expand("Value") does not work !!!
                .ExecuteAsync();

            foreach (TreeNodeRecord treeNode in records)
            {
                Console.WriteLine($"[{treeNode.GetType()}] {{{treeNode.Identity}}} {treeNode.Name}");

                ODataSource source = new(new Uri("https://localhost:5001/home"));
                IEnumerable<TreeNodeRecord> result = await source.TreeNodes
                    .AddQueryOption("$filter", "Identity eq " + treeNode.Identity.ToString().ToLowerInvariant())
                    .Expand("Value")
                    .ExecuteAsync();

                foreach (var item in result)
                {
                    Console.WriteLine($"[{item.GetType()}] {{{item.Identity}}} {item.Name}");

                    if (item.Value is PipelineRecord pipeline)
                    {
                        Console.WriteLine($"{item.Name} = [{item.Value.GetType()}] {pipeline.Name}");
                    }
                    else if (item.Value is PipelineBlockRecord block)
                    {
                        Console.WriteLine($"{item.Name} = [{item.Value.GetType()}] {block.Name}");
                    }
                }
            }
        }
    }
}