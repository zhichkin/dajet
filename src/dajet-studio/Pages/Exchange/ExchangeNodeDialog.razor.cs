using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages.Exchange
{
    public partial class ExchangeNodeDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public TreeNodeModel Model { get; set; } = new();
        private void CloseDialog()
        {
            MudDialog.Cancel();
        }
        private void CreatePipeline()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ExchangeDialogCommand.CreatePipeline
            };

            MudDialog.Close(result);
        }
        private void DeleteExchange()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ExchangeDialogCommand.DeleteExchange
            };

            MudDialog.Close(result);
        }
    }
}