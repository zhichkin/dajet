using DaJet.Studio.Controllers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DaJet.Studio.Components.Metadata
{
    public partial class MetadataObjectContextMenu : ComponentBase, IDisposable
    {
        protected string Title { get; set; }
        protected TreeNodeModel Model { get; set; }
        protected override void OnInitialized()
        {
            if (MdTreeViewController is not null)
            {
                MdTreeViewController.OpenEntityNodeContextMenuHandler = OpenEntityNodeContextMenuHandler;
            }
        }
        private async Task OpenEntityNodeContextMenuHandler(TreeNodeModel node, ElementReference source)
        {
            Model = node;
            Title = node.Url;

            StateHasChanged();

            await JSRuntime.InvokeVoidAsync("OpenMetadataObjectContextMenu", source);
        }
        public void Dispose()
        {
            if (MdTreeViewController is not null)
            {
                MdTreeViewController.OpenEntityNodeContextMenuHandler = null;
            }
        }
        private async Task CloseContextMenu()
        {
            await JSRuntime.InvokeVoidAsync("CloseMetadataObjectContextMenu");
        }
        private async Task OpenMetadataObjectPage(MouseEventArgs args)
        {
            await CloseContextMenu();

            MdTreeViewController.NavigateToMetadataObjectPage(Model);
        }
    }
}