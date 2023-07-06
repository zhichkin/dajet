using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages.Exchange
{
    public partial class ArticleDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public TreeNodeModel Model { get; set; } = new();
        private void CloseDialog()
        {
            MudDialog.Cancel();
        }
        private void EnableArticle()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ExchangeDialogCommand.EnableArticle
            };

            MudDialog.Close(result);
        }
        private void DisableArticle()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ExchangeDialogCommand.DisableArticle
            };

            MudDialog.Close(result);
        }
        private void DeleteArticle()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ExchangeDialogCommand.DeleteArticle
            };

            MudDialog.Close(result);
        }
    }
}