using DaJet.Studio.Components;
using DaJet.Studio.Model;
using System.Net.Http.Json;

namespace DaJet.Studio.Controllers
{
    public sealed class ApiTreeViewController
    {
        private HttpClient Http { get; set; }
        public ApiTreeViewController(HttpClient http)
        {
            Http = http;
        }
        public TreeNodeModel CreateRootNode(InfoBaseModel model)
        {
            return new TreeNodeModel()
            {
                Tag = model,
                Url = $"/api/{model.Name}",
                Title = "api",
                OpenNodeHandler = OpenApiNodeHandler,
                ContextMenuHandler = ContextMenuHandler
            };
        }
        private async Task ContextMenuHandler(TreeNodeModel root)
        {
            await OpenFolderContextMenu(root);
        }
        private async Task OpenApiNodeHandler(TreeNodeModel root)
        {
            if (root == null || root.Nodes.Count > 0)
            {
                return;
            }

            HttpResponseMessage response = await Http.GetAsync(root.Url);

            List<ScriptModel> list = await response.Content.ReadFromJsonAsync<List<ScriptModel>>();

            foreach (ScriptModel model in list)
            {
                root.Nodes.Add(CreateScriptNodeTree(root, model));
            }
        }
        private TreeNodeModel CreateScriptNodeTree(TreeNodeModel parent, ScriptModel model)
        {
            TreeNodeModel node = new()
            {
                Tag = model,
                Title = model.Name,
                Url = $"{parent.Url}/{model.Name}",
                ContextMenuHandler = (model.IsFolder ? OpenFolderContextMenu : OpenScriptContextMenu)
            };

            foreach (ScriptModel child in model.Children)
            {
                node.Nodes.Add(CreateScriptNodeTree(node, child));
            }

            return node;
        }
        private async Task OpenFolderContextMenu(TreeNodeModel root)
        {

        }
        private async Task OpenScriptContextMenu(TreeNodeModel root)
        {

        }
    }
}