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
                MdTreeViewController.OpenMetadataObjectContextMenuHandler = OpenContextMenu;
            }
        }
        private async Task OpenContextMenu(TreeNodeModel node, ElementReference source)
        {
            Model = node;
            Title = node.Url;

            StateHasChanged();

            await JSRuntime.InvokeVoidAsync("OpenMetadataObjectContextMenu", source);
        }
        private async Task CloseContextMenu()
        {
            await JSRuntime.InvokeVoidAsync("CloseMetadataObjectContextMenu");
        }
        public void Dispose()
        {
            if (MdTreeViewController is not null)
            {
                MdTreeViewController.OpenMetadataObjectContextMenuHandler = null;
            }
        }
        private async Task OpenMetadataObjectPage(MouseEventArgs args)
        {
            await CloseContextMenu();

            MdTreeViewController.NavigateToMetadataObjectPage(Model);
        }
    }
}