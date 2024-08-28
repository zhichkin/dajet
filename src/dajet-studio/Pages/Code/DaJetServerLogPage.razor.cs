using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Code
{
    public partial class DaJetServerLogPage : ComponentBase
    {
        protected string Content { get; set; } = string.Empty;
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override async Task OnInitializedAsync()
        {
            Content = await DaJetClient.GetServerLog();
        }
    }
}