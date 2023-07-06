using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages.Exchange
{
    public partial class ArticleTypeDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public TreeNodeModel Model { get; set; } = new();
        public string ArticleName { get; set; } = string.Empty;
        private void CloseDialog()
        {
            MudDialog.Cancel();
        }
        private void CreateArticle()
        {
            ExchangeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                ArticleName = ArticleName,
                CommandType = ExchangeDialogCommand.CreateArticle
            };

            MudDialog.Close(result);
        }
    }
}