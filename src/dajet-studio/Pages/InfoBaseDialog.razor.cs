using DaJet.Model;
using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages
{
    public partial class InfoBaseDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public MainTreeView MainTreeView { get; set; }
        [Parameter] public TreeNodeModel TreeNode { get; set; }
        [Parameter] public InfoBaseRecord Model { get; set; }
        private void Cancel()
        {
            MudDialog.Cancel();
        }
        private void Submit()
        {
            MudDialog.Close(DialogResult.Ok(Model));
        }
        private async Task Refresh()
        {
            try
            {
                HttpResponseMessage response = await Http.GetAsync($"/md/reset/{Model.Name}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(response.ReasonPhrase);
                }

                Snackbar.Add($"Кэш базы данных [{Model}] обновлён успешно.", Severity.Success);

                if (MainTreeView != null && TreeNode != null && Model != null)
                {
                    TreeNode.Nodes.Clear();
                    MainTreeView.ConfigureInfoBaseNode(TreeNode, Model);
                }
            }
            catch (Exception error)
            {
                Snackbar.Add($"Ошибка! [{Model}]: {error.Message}", Severity.Error);
            }

            MudDialog.Cancel();
        }
    }
}