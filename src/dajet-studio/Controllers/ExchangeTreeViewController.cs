using DaJet.Studio.Components;
using DaJet.Studio.Model;
using DaJet.Studio.Pages;
using DaJet.Studio.Pages.Exchange;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net;
using System.Net.Http.Json;

namespace DaJet.Studio.Controllers
{
    public sealed class ExchangeTreeViewController
    {
        private HttpClient Http { get; set; }
        private AppState AppState { get; set; }
        private NavigationManager Navigator { get; set; }
        public ExchangeTreeViewController(AppState appState, HttpClient http, NavigationManager navigator)
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
                Url = $"/exchange/{model.Name}",
                Title = "exchange",
                OpenNodeHandler = OpenRootNodeHandler,
                ContextMenuHandler = ContextMenuHandler
            };
        }
        private async Task OpenRootNodeHandler(TreeNodeModel root)
        {
            if (root == null || root.Nodes.Count > 0)
            {
                return;
            }

            if (root.Tag is not InfoBaseModel)
            {
                return;
            }

            HttpResponseMessage response = await Http.GetAsync(root.Url);

            List<ScriptModel> list = await response.Content.ReadFromJsonAsync<List<ScriptModel>>();

            foreach (ScriptModel model in list)
            {
                TreeNodeModel node = CreateScriptNodeTree(in root, in model);
                node.Parent = root;
                root.Nodes.Add(node);
            }
        }
        private async Task ContextMenuHandler(TreeNodeModel node, IDialogService dialogService)
        {
            if (node.Tag is InfoBaseModel)
            {
                await OpenExchangeRootContextMenu(node, dialogService);
            }
            else if (node.Tag is ScriptModel script && !script.IsFolder)
            {
                Navigator.NavigateTo($"/script-editor/{script.Uuid}");
            }
        }
        private TreeNodeModel CreateScriptNodeTree(in TreeNodeModel parent, in ScriptModel model)
        {
            string url = string.Empty;

            url = $"{parent.Url}/{model.Name}";

            TreeNodeModel node = new()
            {
                Url = url,
                Tag = model,
                Parent = parent,
                Title = model.Name,
                UseToggle = model.IsFolder,
                ContextMenuHandler = ContextMenuHandler
            };

            foreach (ScriptModel script in model.Children)
            {
                TreeNodeModel child = CreateScriptNodeTree(in node, in script);
                child.Parent = node;
                node.Nodes.Add(child);
            }

            return node;
        }
        
        private async Task OpenExchangeRootContextMenu(TreeNodeModel node, IDialogService dialogService)
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
            var dialog = dialogService.Show<ExchangeRootDialog>(node.Url, parameters, options);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }

            if (result.Data is not ExchangeDialogResult dialogResult)
            {
                return;
            }

            if (dialogResult.CommandType == ExchangeDialogCommand.SelectExchange)
            {
                await SelectExchange(node, dialogService);
            }
        }
        private async Task SelectExchange(TreeNodeModel node, IDialogService dialogService)
        {
            if (node.Tag is not InfoBaseModel infobase) { return; }

            DialogParameters parameters = new()
            {
                { "Model", node }
            };
            var settings = new DialogOptions() { CloseButton = true };
            var dialog = dialogService.Show<SelectPublicationDialog>("Select publication", parameters, settings);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }
            if (result.Data is not string name) { return; }

            foreach (TreeNodeModel child in node.Nodes)
            {
                if (child.Title == name)
                {
                    AppState.FooterText = $"{name} exists!"; return;
                }
            }

            bool success = await CreatePublication(infobase, name);
            if (!success) { return; }

            string url = $"{node.Url}/{name}";

            ScriptModel model = new()
            {
                Name = name
            };

            TreeNodeModel publication = new()
            {
                Url = url,
                Tag = model,
                Parent = node,
                Title = name,
                UseToggle = true,
                ContextMenuHandler = ContextMenuHandler
            };

            node.Nodes.Add(publication);
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
            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseModel>(in node);

            if (root == null || root.Tag is not InfoBaseModel infobase)
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
                script.Owner = infobase.Uuid;
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
                ContextMenuHandler = ContextMenuHandler
            };

            node.Nodes.Add(child);
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

        private async Task<bool> CreatePublication(InfoBaseModel infobase, string name)
        {
            try
            {
                HttpResponseMessage response = await Http.PostAsJsonAsync($"/exchange/{infobase.Name}/{name}", name);

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
    }
}