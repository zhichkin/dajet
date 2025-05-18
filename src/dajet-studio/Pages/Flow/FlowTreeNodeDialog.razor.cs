using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
        private void SelectFolder()
        {
            TreeNodeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = TreeNodeDialogCommand.SelectFolder
            };

            MudDialog.Close(result);
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
        private void DeletePipeline()
        {
            TreeNodeDialogResult result = new(Model, typeof(TreeNodeModel), false)
            {
                CommandType = TreeNodeDialogCommand.DeleteEntity
            };

            MudDialog.Close(result);
        }
        protected void OpenKafkaProducerPage(MouseEventArgs args)
        {
            Navigator.NavigateTo("/create-kafka-producer");
        }
        protected void OpenKafkaConsumerPage(MouseEventArgs args)
        {
            Navigator.NavigateTo("/create-kafka-consumer");
        }
    }
}