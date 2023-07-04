using DaJet.Flow.Model;
using DaJet.Studio.Components;
using DaJet.Studio.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages.Exchange
{
    public partial class SelectPublicationDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public TreeNodeModel Model { get; set; } = new();
        protected List<string> Items { get; set; } = new();
        protected override async Task OnInitializedAsync()
        {
            Items = await GetPublications();
        }
        protected void CloseDialog() { MudDialog.Cancel(); }
        protected void OnSelectItem(TableRowClickEventArgs<string> args)
        {
            if (args.Item is string name)
            {
                MudDialog.Close(name);
            }
        }
        private async Task<List<string>> GetPublications()
        {
            List<string> list = new();

            if (Model.Tag is not InfoBaseModel infobase)
            {
                return list;
            }

            try
            {
                HttpResponseMessage response = await Http.GetAsync($"/exchange/{infobase.Name}/publications");

                if (!response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    //ErrorText = response.ReasonPhrase
                    //    + (string.IsNullOrEmpty(result)
                    //    ? string.Empty
                    //    : Environment.NewLine + result);
                }
                else
                {
                    list = await response.Content.ReadFromJsonAsync<List<string>>();
                }
            }
            catch (Exception error)
            {
                //ErrorText = error.Message;
            }

            return list;
        }
    }
}