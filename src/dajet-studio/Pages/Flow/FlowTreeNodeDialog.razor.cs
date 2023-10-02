using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DaJet.Studio.Pages.Flow
{
    public partial class FlowTreeNodeDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        [Parameter] public TreeNodeModel Model { get; set; } = new();
        private void CloseDialog()
        {
            MudDialog.Cancel();
        }
        private void CreateFolder()
        {
            TreeNodeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = TreeNodeDialogCommand.CreateFolder
            };

            MudDialog.Close(result);
        }
        private void OpenPipelineTable()
        {
            TreeNodeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = TreeNodeDialogCommand.UpdateFolder
            };

            MudDialog.Close(result);
        }
        private void CreatePipeline()
        {
            TreeNodeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = TreeNodeDialogCommand.CreateEntity
            };

            MudDialog.Close(result);
        }
        private void UpdatePipeline()
        {
            TreeNodeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = TreeNodeDialogCommand.UpdateEntity
            };

            MudDialog.Close(result);
        }
        private void DeleteFolder()
        {
            TreeNodeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = TreeNodeDialogCommand.DeleteFolder
            };

            MudDialog.Close(result);
        }
    }
}