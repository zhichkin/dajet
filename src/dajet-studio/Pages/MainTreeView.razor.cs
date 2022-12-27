using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages
{
    public partial class MainTreeView : ComponentBase
    {
        [Inject] protected HttpClient Http { get; set; }
        [Inject] protected IJSRuntime JSRuntime { get; set; }
        [Inject] protected NavigationManager Navigator { get; set; }
        protected HashSet<TreeNodeModel> RootNodes { get; set; } = new();
        protected override async Task OnInitializedAsync()
        {
            RootNodes.Clear();

            try
            {
                HttpResponseMessage response = await Http.GetAsync("md");

                List<InfoBaseModel> list = await response.Content.ReadFromJsonAsync<List<InfoBaseModel>>();

                foreach (InfoBaseModel item in list)
                {
                    RootNodes.Add(new TreeNodeModel()
                    {
                        Model = item,
                        Title = item.Name,
                        Icon = Icons.Filled.Folder,
                        CanExpand = true
                    });
                }
            }
            catch (Exception error)
            {
                RootNodes.Add(new TreeNodeModel()
                {
                    Icon = Icons.Filled.Error,
                    Title = "Ошибка загрузки данных!"
                });
            }
        }
        public async Task<HashSet<TreeNodeModel>> GetTreeNodeItems(TreeNodeModel parent)
        {
            await Task.Delay(1000);

            return new HashSet<TreeNodeModel>();

            //bool confirmed = await JSRuntime.InvokeAsync<bool>("confirm", "Click");

            //if (confirmed)
            //{
            //    string prompt = await JSRuntime.InvokeAsync<string>("prompt", "Введите что-нибудь:");
            //}

            //try
            //{
            //    HttpResponseMessage response = await Http.GetAsync("md");

            //    string content = await response.Content.ReadAsStringAsync();

            //    await JSRuntime.InvokeVoidAsync("alert", content);
            //}
            //catch (Exception error)
            //{
            //    await JSRuntime.InvokeVoidAsync("alert", error.Message);
            //}

            //Stream stream = await response.Content.ReadAsStreamAsync();
            //List<VirtualHostInfo> list = await JsonSerializer.DeserializeAsync<List<VirtualHostInfo>>(stream);

            //Navigator.NavigateTo("/");
        }
    }
}