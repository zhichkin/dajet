using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages
{
    public partial class DbViewDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public TreeNodeModel Model { get; set; } = new();
        public string SchemaName { get; set; } = string.Empty;
        private void CloseDialog()
        {
            MudDialog.Cancel();
        }
        private void CreateCommand()
        {
            DbViewDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                SchemaName = SchemaName,
                CommandType = DbViewDialogCommand.Create
            };
            MudDialog.Close(result);
        }
        private void UpdateCommand()
        {
            DbViewDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = DbViewDialogCommand.Update
            };
            MudDialog.Close(result);
        }
        private void DeleteCommand()
        {
            DbViewDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                SchemaName = SchemaName,
                CommandType = DbViewDialogCommand.Delete
            };
            MudDialog.Close(result);
        }
    }
}