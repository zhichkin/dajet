using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages
{
    public partial class DbSchemaDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public TreeNodeModel Model { get; set; } = new();
        private void CloseDialog()
        {
            MudDialog.Cancel();
        }
        private void ScriptFileCommand()
        {
            DbSchemaDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = DbSchemaDialogCommand.Script
            };
            MudDialog.Close(result);
        }
        private void CreateCommand()
        {
            DbSchemaDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = DbSchemaDialogCommand.Create
            };
            MudDialog.Close(result);
        }
        private void UpdateCommand()
        {
            DbSchemaDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = DbSchemaDialogCommand.Update
            };
            MudDialog.Close(result);
        }
        private void DeleteCommand()
        {
            DbSchemaDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = DbSchemaDialogCommand.Delete
            };
            MudDialog.Close(result);
        }
    }
}