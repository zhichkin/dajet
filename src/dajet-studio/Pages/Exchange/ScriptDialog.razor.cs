using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages.Exchange
{
    public partial class ScriptDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public TreeNodeModel Model { get; set; } = new();
        private void CloseDialog()
        {
            MudDialog.Cancel();
        }
        private void EnableScript()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ExchangeDialogCommand.EnableScript
            };

            MudDialog.Close(result);
        }
        private void DisableScript()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ExchangeDialogCommand.DisableScript
            };

            MudDialog.Close(result);
        }
        private void OpenScriptInEditor()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ExchangeDialogCommand.OpenScriptInEditor
            };

            MudDialog.Close(result);
        }
    }
}