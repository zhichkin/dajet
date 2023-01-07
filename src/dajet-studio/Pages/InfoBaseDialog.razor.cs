using DaJet.Studio.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages
{
    public partial class InfoBaseDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public InfoBaseModel Model { get; set; } = new();
        private void Cancel()
        {
            MudDialog.Cancel();
        }
        private void Submit()
        {
            MudDialog.Close(DialogResult.Ok(Model));
        }
    }
}