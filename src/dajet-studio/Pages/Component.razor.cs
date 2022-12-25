using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DaJet.Studio.Pages
{
    public partial class Component : ComponentBase
    {
        [Inject] protected HttpClient Http { get; set; }
        [Inject] protected IJSRuntime JSRuntime { get; set; }
        [Inject] protected NavigationManager Navigator { get; set; }
        protected async Task Click()
        {
            bool confirmed = await JSRuntime.InvokeAsync<bool>("confirm", "Click");
            
            if (confirmed)
            {
                string prompt = await JSRuntime.InvokeAsync<string>("prompt", "Введите что-нибудь:");
            }

            try
            {
                HttpResponseMessage response = await Http.GetAsync("md");

                string content = await response.Content.ReadAsStringAsync();

                await JSRuntime.InvokeVoidAsync("alert", content);
            }
            catch (Exception error)
            {
                await JSRuntime.InvokeVoidAsync("alert", error.Message);
            }

            //Stream stream = await response.Content.ReadAsStreamAsync();
            //List<VirtualHostInfo> list = await JsonSerializer.DeserializeAsync<List<VirtualHostInfo>>(stream);

            Navigator.NavigateTo("/");
        }
    }
}