using DaJet.Model;
using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages
{
    public partial class ScriptTreeNodeDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public TreeNodeModel Model { get; set; } = new();
        private void CloseDialog()
        {
            MudDialog.Cancel();
        }
        private void CreateFolder()
        {
            ScriptTreeNodeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ScriptTreeNodeDialogCommand.CreateFolder
            };

            MudDialog.Close(result);
        }
        private void CreateScript()
        {
            ScriptTreeNodeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ScriptTreeNodeDialogCommand.CreateScript
            };

            MudDialog.Close(result);
        }
        private void UpdateScript()
        {
            if (Model.Tag is not ScriptRecord)
            {
                return;
            }

            ScriptTreeNodeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = ScriptTreeNodeDialogCommand.UpdateScript
            };

            MudDialog.Close(result);
        }
        private void DeleteFolderOrScript()
        {
            if (Model.Tag is not ScriptRecord script)
            {
                return;
            }

            ScriptTreeNodeDialogCommand command = script.IsFolder
                    ? ScriptTreeNodeDialogCommand.DeleteFolder
                    : ScriptTreeNodeDialogCommand.DeleteScript;

            ScriptTreeNodeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = command
            };

            MudDialog.Close(result);
        }
    }
}