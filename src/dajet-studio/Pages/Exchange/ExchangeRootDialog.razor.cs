using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages.Exchange
{
    public partial class ExchangeRootDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public TreeNodeModel Model { get; set; } = new();
        private void CloseDialog()
        {
            MudDialog.Cancel();
        }
        private void SelectExchange()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ExchangeDialogCommand.SelectExchange
            };

            MudDialog.Close(result);
        }
        private void ConfigureRabbitMQ()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ExchangeDialogCommand.DeleteVirtualHostRabbitMQ
            };

            MudDialog.Close(result);
        }
        private void ExchangeTuning()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ExchangeDialogCommand.ExchangeTuning
            };

            MudDialog.Close(result);
        }
    }
}