using DaJet.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages
{
    public partial class InfoBaseDialog : ComponentBase
    {
        [Parameter] public InfoBaseRecord Model { get; set; }
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
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