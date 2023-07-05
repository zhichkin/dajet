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
            else if (node.Parent is not null && node.Parent.Tag is InfoBaseModel)
            {
                await OpenExchangeNodeContextMenu(node, dialogService);
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
        private async Task OpenExchangeNodeContextMenu(TreeNodeModel node, IDialogService dialogService)
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
            var dialog = dialogService.Show<ExchangeNodeDialog>(node.Url, parameters, options);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }

            if (result.Data is not ExchangeDialogResult dialogResult)
            {
                return;
            }

            if (dialogResult.CommandType == ExchangeDialogCommand.CreatePipeline)
            {
                await CreatePipeline(node, dialogService);
            }
            else if (dialogResult.CommandType == ExchangeDialogCommand.DeleteExchange)
            {
                await DeleteExchange(node, dialogService);
            }
        }
        private async Task SelectExchange(TreeNodeModel root, IDialogService dialogService)
        {
            if (root.Tag is not InfoBaseModel infobase) { return; }

            DialogParameters parameters = new()
            {
                { "Model", root }
            };
            var settings = new DialogOptions() { CloseButton = true };
            var dialog = dialogService.Show<SelectPublicationDialog>("Select publication", parameters, settings);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }
            if (result.Data is not string name) { return; }

            foreach (TreeNodeModel child in root.Nodes)
            {
                if (child.Title == name)
                {
                    AppState.FooterText = $"{name} exists!"; return;
                }
            }

            bool success = await CreatePublication(infobase, name);
            if (!success) {/* CORS error may happen */ }

            HttpResponseMessage response = await Http.GetAsync($"{root.Url}/{name}");

            ScriptModel model = await response.Content.ReadFromJsonAsync<ScriptModel>();

            TreeNodeModel node = CreateScriptNodeTree(in root, in model);
            node.Parent = root;
            root.Nodes.Add(node);
        }
        private async Task<bool> CreatePublication(InfoBaseModel infobase, string name)
        {
            try
            {
                HttpResponseMessage response = await Http.PostAsync($"/exchange/{infobase.Name}/{name}", null);

                if (response.StatusCode == HttpStatusCode.Created)
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
        private async Task DeleteExchange(TreeNodeModel node, IDialogService dialogService)
        {
            if (node.Parent is null ||
                node.Parent.Tag is not InfoBaseModel infobase ||
                node.Tag is not ScriptModel script)
            {
                return;
            }

            bool success = await DeletePublication(infobase, node.Title);

            if (!success)
            {
                return;
            }

            node.Parent.Nodes.Remove(node);

            node.IsVisible = false;
        }
        private async Task<bool> DeletePublication(InfoBaseModel infobase, string name)
        {
            try
            {
                HttpResponseMessage response = await Http.DeleteAsync($"/exchange/{infobase.Name}/{name}");

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
        private async Task DeleteArticle(TreeNodeModel node)
        {
            if (node.Tag is not ScriptModel script)
            {
                return;
            }

            bool success = await DeleteArticle(script);

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
        private async Task<bool> DeleteArticle(ScriptModel script)
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
        private async Task<bool> CreatePipeline(TreeNodeModel node, IDialogService dialogService)
        {
            return true;

            //try
            //{
            //    HttpResponseMessage response = await Http.PostAsJsonAsync($"/exchange/{infobase.Name}/{name}", name);

            //    if (response.StatusCode != HttpStatusCode.Created)
            //    {
            //        AppState.FooterText = response.ReasonPhrase;
            //        return false;
            //    }
            //    return true;
            //}
            //catch (Exception error)
            //{
            //    AppState.FooterText = error.Message;
            //    return false;
            //}
        }
    }
}