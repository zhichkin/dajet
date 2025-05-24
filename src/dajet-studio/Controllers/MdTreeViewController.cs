using DaJet.Http.Model;
using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Controllers
{
    public sealed class MdTreeViewController
    {
        private readonly NavigationManager Navigator;
        public Func<TreeNodeModel, ElementReference, Task> OpenEntityNodeContextMenuHandler { get; set; }
        public MdTreeViewController(NavigationManager navigator)
        {
            Navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        }
        public void NavigateToMetadataObjectPage(in TreeNodeModel model)
        {
            if (model.Tag is not MetadataItemModel) { return; }

            if (string.IsNullOrWhiteSpace(model.Url)) { return; }

            string url = model.Url.Replace('/', '~');

            Navigator.NavigateTo($"/metadata-object-page/{url}");
        }
        public Task ShowEntityNodeContextMenu(TreeNodeModel node, ElementReference source)
        {
            if (node.Tag is not MetadataItemModel model)
            {
                return Task.CompletedTask;
            }

            OpenEntityNodeContextMenuHandler?.Invoke(node, source);

            return Task.CompletedTask;
        }
    }
}