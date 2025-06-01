using DaJet.Studio.Controllers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DaJet.Studio.Components.Metadata
{
    public partial class InfoBaseContextMenu : ComponentBase, IDisposable
    {
        protected string Title { get; set; }
        protected TreeNodeModel Model { get; set; }
        protected override void OnInitialized()
        {
            if (MdTreeViewController is not null)
            {
                MdTreeViewController.OpenInfoBaseContextMenuHandler = OpenContextMenu;
            }
        }
        private async Task OpenContextMenu(TreeNodeModel node, ElementReference source)
        {
            Model = node;
            Title = node.Title;

            StateHasChanged();

            await JSRuntime.InvokeVoidAsync("OpenInfoBaseContextMenu", source);
        }
        private async Task CloseContextMenu()
        {
            await JSRuntime.InvokeVoidAsync("CloseInfoBaseContextMenu");
        }
        public void Dispose()
        {
            if (MdTreeViewController is not null)
            {
                MdTreeViewController.OpenInfoBaseContextMenuHandler = null;
            }
        }
        private async Task ClearInfoBaseMetadataCache()
        {
            await CloseContextMenu();
            await MdTreeViewController.ClearInfoBaseMetadataCache(Model);
        }
        private async Task OpenInfoBaseSettingsPage()
        {
            await CloseContextMenu();
            await MdTreeViewController.OpenInfoBaseSettingsDialog(Model);
        }
        private async Task OpenMetadataDiagnosticPage(MouseEventArgs args)
        {
            await CloseContextMenu();
            MdTreeViewController.NavigateToMetadataDiagnosticPage(Model);
        }
        private async Task OpenDbViewGeneratorPage(MouseEventArgs args)
        {
            await CloseContextMenu();
            MdTreeViewController.NavigateToDbViewGeneratorPage(Model);
        }
    }
}