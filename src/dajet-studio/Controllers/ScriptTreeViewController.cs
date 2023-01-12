using DaJet.Studio.Components;
using DaJet.Studio.Model;
using DaJet.Studio.Pages;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net;
using System.Net.Http.Json;

namespace DaJet.Studio.Controllers
{
    public sealed class ApiTreeViewController
    {
        private HttpClient Http { get; set; }
        private AppState AppState { get; set; }
        private NavigationManager Navigator { get; set; }
        public ApiTreeViewController(AppState appState, HttpClient http, NavigationManager navigator)
        {
            Http = http;
            AppState = appState;
            Navigator = navigator;
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
        private async Task ContextMenuHandler(TreeNodeModel root, IDialogService dialogService)
        {
            await OpenContextMenu(root, dialogService);
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
                TreeNodeModel node = CreateScriptNodeTree(root, model);
                node.Parent = root;
                root.Nodes.Add(node);
            }
        }
        private TreeNodeModel CreateScriptNodeTree(TreeNodeModel parent, ScriptModel model)
        {
            TreeNodeModel node = new()
            {
                Tag = model,
                Parent = parent,
                Title = model.Name,
                UseToggle = model.IsFolder,
                Url = $"{parent.Url}/{model.Name}",
                ContextMenuHandler = OpenContextMenu
            };

            foreach (ScriptModel script in model.Children)
            {
                TreeNodeModel child = CreateScriptNodeTree(node, script);
                child.Parent = node;
                node.Nodes.Add(child);
            }

            return node;
        }
        private async Task OpenContextMenu(TreeNodeModel node, IDialogService dialogService)
        {
            DialogParameters parameters = new()
            {
                { "Model", node }
            };
            DialogOptions options = new()
            {
                NoHeader = true,
                CloseButton = false,
                CloseOnEscapeKey = true,
                DisableBackdropClick = false,
                Position = DialogPosition.Center
            };
            var dialog = dialogService.Show<ScriptTreeNodeDialog>(node.Url, parameters, options);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }

            if (result.Data is not ScriptTreeNodeDialogResult dialogResult)
            {
                return;
            }

            if (dialogResult.CommandType == ScriptTreeNodeDialogCommand.CreateFolder)
            {
                await CreateFolder(node);
            }
            else if (dialogResult.CommandType == ScriptTreeNodeDialogCommand.CreateScript)
            {
                await CreateScript(node);
            }
            else if (dialogResult.CommandType == ScriptTreeNodeDialogCommand.UpdateScript)
            {
                await UpdateScript(node);
            }
            else if (dialogResult.CommandType == ScriptTreeNodeDialogCommand.DeleteFolder)
            {
                await DeleteFolder(node);
            }
            else if (dialogResult.CommandType == ScriptTreeNodeDialogCommand.DeleteScript)
            {
                await DeleteScript(node);
            }
        }
        private async Task CreateFolder(TreeNodeModel node)
        {
            ScriptModel script = new()
            {
                Uuid = Guid.NewGuid().ToString(),
                Name = "NewFolder",
                IsFolder = true
            };

            bool success = await InsertScript(node, script);
            if (!success) { return; }

            if (node.Tag is ScriptModel parent)
            {
                parent.Children.Add(script);
            }

            TreeNodeModel child = new()
            {
                Tag = script,
                Parent = node,
                Title = script.Name,
                UseToggle = true,
                Url = $"{node.Url}/{script.Name}",
                ContextMenuHandler = OpenContextMenu
            };

            node.Nodes.Add(child);
        }
        private async Task CreateScript(TreeNodeModel node)
        {
            ScriptModel script = new()
            {
                Uuid = Guid.NewGuid().ToString(),
                Name = "NewScript",
                IsFolder = false
            };

            bool success = await InsertScript(node, script);
            if (!success) { return; }

            if (node.Tag is ScriptModel parent)
            {
                parent.Children.Add(script);
            }

            TreeNodeModel child = new()
            {
                Tag = script,
                Parent = node,
                Title = script.Name,
                UseToggle = false,
                Url = $"{node.Url}/{script.Name}",
                ContextMenuHandler = OpenContextMenu
            };

            node.Nodes.Add(child);
        }
        private async Task UpdateScript(TreeNodeModel node)
        {
            if (node.Tag is ScriptModel script)
            {
                Navigator.NavigateTo($"/script-editor/{script.Uuid}");
            }
        }
        private async Task DeleteFolder(TreeNodeModel node)
        {
            if (node.Tag is not ScriptModel script)
            {
                return;
            }

            bool success = await DeleteScript(node, script);
            if (!success)
            {
                return;
            }

            if (node.Parent.Tag is ScriptModel parent && node.Tag is ScriptModel child)
            {
                parent.Children.Remove(child);
            }

            node.Parent.Nodes.Remove(node);

            node.IsVisible = false;
        }
        private async Task DeleteScript(TreeNodeModel node)
        {
            if (node.Tag is not ScriptModel script)
            {
                return;
            }

            bool success = await DeleteScript(node, script);
            if (!success)
            {
                return;
            }

            if (node.Parent.Tag is ScriptModel parent && node.Tag is ScriptModel child)
            {
                parent.Children.Remove(child);
            }

            node.Parent.Nodes.Remove(node);

            node.IsVisible = false;
        }

        private async Task<bool> InsertScript(TreeNodeModel node, ScriptModel script)
        {
            try
            {
                HttpResponseMessage response = await Http.PostAsJsonAsync(node.Url, script);

                if (response.StatusCode != HttpStatusCode.Created)
                {
                    AppState.FooterText = response.ReasonPhrase;
                    return false;
                }
                return true;
            }
            catch (Exception error)
            {
                AppState.FooterText = error.Message;
                return false;
            }
        }
        private async Task<bool> DeleteScript(TreeNodeModel node, ScriptModel script)
        {
            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseModel>(in node);

            if (root == null)
            {
                return false;
            }

            if (root.Tag is not InfoBaseModel database)
            {
                return false;
            }

            string url = $"/api/{database.Name}/{script.Uuid}";

            try
            {
                HttpResponseMessage response = await Http.DeleteAsync(url);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    AppState.FooterText = response.ReasonPhrase;
                    return false;
                }
                return true;
            }
            catch (Exception error)
            {
                AppState.FooterText = error.Message;
                return false;
            }
        }
    }
}