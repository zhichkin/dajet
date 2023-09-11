using DaJet.Data;
using DaJet.Http.Client;
using DaJet.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static MudBlazor.Colors;

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
            DaJetHttpClient client = DataSource as DaJetHttpClient;

            List<TreeNodeRecord> nodes = await client.SelectTreeNodes();

            foreach (TreeNodeRecord node in nodes)
            {
                if (node.Value is TreeNodeRecord value)
                {
                    if (value.State == PersistentState.Virtual)
                    {
                        Console.WriteLine($"{node} : {node.Parent} {{{value.State}}} [{value.Name}]");

                        await node.Value.LoadAsync();

                        Console.WriteLine($"{node} : {node.Parent} {{{value.State}}} [{value.Name}]");
                    }
                }

                //if (node.Value is TreeNodeRecord value)
                //{
                //    Console.WriteLine($"{node} : {node.Parent} {{{value.State}}} [{value.Name}]");

                //    value.Load();

                //    Console.WriteLine($"{node} : {node.Parent} {{{value.State}}} [{value.Name}]");

                //    node.Value = DomainModel.New<TreeNodeRecord>(value.Identity);

                //    value = node.Value as TreeNodeRecord;

                //    Console.WriteLine($"{node} : {node.Parent} {{{value.State}}} [{value.Name}]");
                //}
                //else
                //{
                //    Console.WriteLine($"{node} : {node.Parent} [{node.Value}]");
                //}
            }
        }
    }
}