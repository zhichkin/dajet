using DaJet.Http.Client;
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
            DaJetHttpClient client = DataSource as DaJetHttpClient;

            List<TreeNodeRecord> nodes = await client.SelectTreeNodes();

            foreach (TreeNodeRecord node in nodes)
            {
                TreeNodeRecord value = await client.SelectAsync(node.Value) as TreeNodeRecord;

                value.Name = "new name";
                //value.Name = node.Name; // does not change !

                Console.WriteLine($"{node.Name} ({node.IsOriginal()}) [{value.Name}] {value.IsChanged()}");
            }
        }
    }
}