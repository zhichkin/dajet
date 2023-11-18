using DaJet.Model;
using DaJet.Studio.Components;
using DaJet.Studio.Model;
using DaJet.Studio.Pages;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;

namespace DaJet.Studio.Controllers
{
    public sealed class ApiTreeViewController
    {
        private HttpClient Http { get; set; }
        private AppState AppState { get; set; }
        private NavigationManager Navigator { get; set; }
        private readonly Func<TreeNodeModel, CancelEventArgs, Task> _updateTitleCommandHandler;
        public ApiTreeViewController(AppState appState, HttpClient http, NavigationManager navigator)
        {
            Http = http;
            AppState = appState;
            Navigator = navigator;

            _updateTitleCommandHandler = new(UpdateTitleCommandHandler);
        }
        public TreeNodeModel CreateRootNode(InfoBaseRecord model)
        {
            return new TreeNodeModel()
            {
                Tag = model,
                Url = $"/api/select/{model.Name}",
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

            if (root.Tag is not InfoBaseRecord)
            {
                return;
            }

            HttpResponseMessage response = await Http.GetAsync(root.Url);

            List<ScriptModel> list = await response.Content.ReadFromJsonAsync<List<ScriptModel>>();

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Name == "exchange")
                {
                    list.RemoveAt(i); break;
                }
            }

            foreach (ScriptModel model in list)
            {
                TreeNodeModel node = CreateScriptNodeTree(in root, in model);
                node.Parent = root;
                root.Nodes.Add(node);
            }
        }
        private TreeNodeModel CreateScriptNodeTree(in TreeNodeModel parent, in ScriptModel model)
        {
            string url = string.Empty;

            if (parent.Tag is InfoBaseRecord infobase)
            {
                url = $"/api/{infobase.Name}/{model.Name}";
            }
            else
            {
                url = $"{parent.Url}/{model.Name}";
            }

            TreeNodeModel node = new()
            {
                Url = url,
                Tag = model,
                Parent = parent,
                Title = model.Name,
                UseToggle = model.IsFolder,
                CanBeEdited = true,
                ContextMenuHandler = OpenContextMenu,
                UpdateTitleCommand = _updateTitleCommandHandler
            };

            foreach (ScriptModel script in model.Children)
            {
                TreeNodeModel child = CreateScriptNodeTree(in node, in script);
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
                UpdateScript(node);
            }
            else if (dialogResult.CommandType == ScriptTreeNodeDialogCommand.DeleteFolder)
            {
                await DeleteFolderScript(node);
            }
            else if (dialogResult.CommandType == ScriptTreeNodeDialogCommand.DeleteScript)
            {
                await DeleteFolderScript(node);
            }
        }
        private async Task CreateFolder(TreeNodeModel node)
        {
            ScriptModel script = new()
            {
                Name = "NewFolder",
                IsFolder = true
            };

            await CreateFolderScript(node, script);
        }
        private async Task CreateScript(TreeNodeModel node)
        {
            ScriptModel script = new()
            {
                Name = "NewScript",
                IsFolder = false
            };

            await CreateFolderScript(node, script);
        }
        private async Task CreateFolderScript(TreeNodeModel node, ScriptModel script)
        {
            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseRecord>(in node);

            if (root == null || root.Tag is not InfoBaseRecord infobase)
            {
                return; // owner database is not found
            }

            ScriptModel parent = node.Tag as ScriptModel;

            if (parent != null)
            {
                script.Owner = parent.Owner;
                script.Parent = parent.Uuid;
            }
            else
            {
                script.Owner = infobase.Identity;
                script.Parent = Guid.Empty;
            }

            bool success = await InsertScript(script);

            if (!success)
            {
                return;
            }

            string url = string.Empty;

            if (parent == null)
            {
                url = $"/api/{infobase.Name}/{script.Name}";
            }
            else
            {
                parent.Children.Add(script);
                url = $"{node.Url}/{script.Name}";
            }

            TreeNodeModel child = new()
            {
                Url = url,
                Tag = script,
                Parent = node,
                Title = script.Name,
                UseToggle = script.IsFolder,
                CanBeEdited = true,
                ContextMenuHandler = OpenContextMenu,
                UpdateTitleCommand = _updateTitleCommandHandler
            };

            node.Nodes.Add(child);
        }
        private void UpdateScript(TreeNodeModel node)
        {
            if (node.Tag is ScriptModel script)
            {
                Navigator.NavigateTo($"/script-editor/{script.Uuid}");
            }
        }
        private async Task UpdateTitleCommandHandler(TreeNodeModel node, CancelEventArgs args)
        {
            if (node.Tag is not ScriptModel script)
            {
                return;
            }

            bool success = await UpdateScriptName(new ScriptModel()
            {
                Uuid = script.Uuid,
                Name = node.Title
            });

            if (success)
            {
                script.Name = node.Title;

                if (node.Parent is not null)
                {
                    if (node.Parent.Tag is InfoBaseRecord infobase)
                    {
                        node.Url = $"/api/{infobase.Name}/{script.Name}";
                    }
                    else
                    {
                        node.Url = $"{node.Parent.Url}/{script.Name}";
                    }
                }
            }
            else
            {
                args.Cancel = true;
            }
        }
        private async Task DeleteFolderScript(TreeNodeModel node)
        {
            if (node.Tag is not ScriptModel script)
            {
                return;
            }

            bool success = await DeleteScript(script);
            
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

        private async Task<bool> InsertScript(ScriptModel script)
        {
            try
            {
                HttpResponseMessage response = await Http.PostAsJsonAsync($"/api", script);

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
        private async Task<bool> UpdateScriptName(ScriptModel script)
        {
            try
            {
                HttpResponseMessage response = await Http.PutAsJsonAsync($"/api/name", script);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }

                AppState.FooterText = response.ReasonPhrase;
                return false;
            }
            catch (Exception error)
            {
                AppState.FooterText = error.Message;
                return false;
            }
        }
        private async Task<bool> DeleteScript(ScriptModel script)
        {
            try
            {
                HttpResponseMessage response = await Http.DeleteAsync($"/api/{script.Uuid}");

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