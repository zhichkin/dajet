using DaJet.Data;
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

            var list = await client.SelectAsync(10, "Parent", new Entity(10, Guid.Empty));

            foreach (var item in list)
            {
                if (item is not TreeNodeRecord node)
                {
                    continue;
                }

                EntityObject entity = await client.SelectAsync(node.Value);

                if (entity is not TreeNodeRecord value)
                {
                    continue;
                }

                value.Name = "new name";
                //value.Name = node.Name; // does not change !

                Console.WriteLine($"{node.Name} ({node.IsOriginal()}) [{value.Name}] {value.IsChanged()}");
            }
        }
    }
}