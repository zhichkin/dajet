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
            else if (node.Title == "Документ" || node.Title == "Справочник" || node.Title == "РегистрСведений")
            {
                await OpenArticleTypeContextMenu(node, dialogService);
            }
            else if (node.Parent is not null
                && (node.Parent.Title == "Документ" || node.Parent.Title == "Справочник" || node.Parent.Title == "РегистрСведений"))
            {
                await OpenArticleContextMenu(node, dialogService);
            }
            else if (node.Tag is ScriptModel script && !script.IsFolder)
            {
                await OpenScriptContextMenu(node, dialogService);
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
                NavigateToCreatePipelinePage(node, dialogService);
            }
            else if (dialogResult.CommandType == ExchangeDialogCommand.ConfigureRabbitMQ)
            {
                NavigateToConfigureRabbitMQPage(node, dialogService);
            }
            else if (dialogResult.CommandType == ExchangeDialogCommand.DeleteExchange)
            {
                await DeleteExchange(node, dialogService);
            }
        }
        private void NavigateToCreatePipelinePage(TreeNodeModel node, IDialogService dialogService)
        {
            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseModel>(in node);

            if (root is null || root.Tag is not InfoBaseModel database) { return; }

            if (node.Tag is not ScriptModel exchange) { return; }

            Navigator.NavigateTo($"/create-pipeline/{database.Name}/{exchange.Name}");
        }
        private void NavigateToConfigureRabbitMQPage(TreeNodeModel node, IDialogService dialogService)
        {
            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseModel>(in node);

            if (root is null || root.Tag is not InfoBaseModel database) { return; }

            if (node.Tag is not ScriptModel exchange) { return; }

            Navigator.NavigateTo($"/configure-rabbit/{database.Name}/{exchange.Name}");
        }
        private async Task SelectExchange(TreeNodeModel root, IDialogService dialogService)
        {
            if (root.Tag is not InfoBaseModel infobase) { return; }

            DialogParameters parameters = new()
            {
                { "Model", root }
            };
            var settings = new DialogOptions() { CloseButton = true };
            var dialog = dialogService.Show<SelectPublicationDialog>("Выбор...", parameters, settings);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }
            if (result.Data is not string name) { return; }

            foreach (TreeNodeModel child in root.Nodes)
            {
                if (child.Title == name)
                {
                    Navigator.NavigateTo("/error-page");
                    AppState.LastErrorText = $"План обмена \"{name}\" уже добавлен!";
                    return;
                }
            }

            bool success = await CreatePublication(infobase, name);
            
            if (!success)
            {
                Navigator.NavigateTo("/error-page"); return;
            }

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

                AppState.LastErrorText = response.ReasonPhrase;
            }
            catch (Exception error)
            {
                AppState.LastErrorText = error.Message;
            }

            return false;
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
                Navigator.NavigateTo("/error-page"); return;
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
                    AppState.LastErrorText = response.ReasonPhrase;
                }
                return true;
            }
            catch (Exception error)
            {
                AppState.LastErrorText = error.Message;
            }
            return false;
        }

        private async Task OpenArticleTypeContextMenu(TreeNodeModel node, IDialogService dialogService)
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
            var dialog = dialogService.Show<ArticleTypeDialog>(node.Url, parameters, options);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }

            if (result.Data is not ExchangeDialogResult dialogResult)
            {
                return;
            }

            if (dialogResult.CommandType == ExchangeDialogCommand.CreateArticle)
            {
                await CreateArticle(node, dialogResult.ArticleName);
            }
        }
        private async Task CreateArticle(TreeNodeModel node, string articleName)
        {
            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseModel>(in node);

            if (root is null || root.Tag is not InfoBaseModel database)
            {
                return; // owner database is not found
            }

            if (node.Tag is not ScriptModel)
            {
                return;
            }

            ScriptModel script = await CreateArticle(database.Name, node.Parent.Parent.Title, node.Title, articleName);

            if (script is null)
            {
                Navigator.NavigateTo("/error-page");
                AppState.LastErrorText = $"Объект метаданных \"{node.Title}.{articleName}\" уже добавлен, не входит в состав плана обмена или не существует.";
                return;
            }

            TreeNodeModel child = CreateScriptNodeTree(in node, in script);
            child.Parent = node;
            node.Nodes.Add(child);
        }
        private async Task<ScriptModel> CreateArticle(string database, string publication, string type, string article)
        {
            try
            {
                string url = $"/exchange/{database}/{publication}/{type}/{article}";

                HttpResponseMessage response = await Http.PostAsync(url, null);

                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return await response.Content.ReadFromJsonAsync<ScriptModel>();
                }

                AppState.LastErrorText = response.ReasonPhrase;
            }
            catch (Exception error)
            {
                AppState.LastErrorText = error.Message;
            }

            return null;
        }

        private async Task OpenArticleContextMenu(TreeNodeModel node, IDialogService dialogService)
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
            var dialog = dialogService.Show<ArticleDialog>(node.Url, parameters, options);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }

            if (result.Data is not ExchangeDialogResult dialogResult)
            {
                return;
            }

            if (dialogResult.CommandType == ExchangeDialogCommand.DeleteArticle)
            {
                await DeleteArticle(node);
            }
            else if (dialogResult.CommandType == ExchangeDialogCommand.EnableArticle)
            {
                await EnableArticle(node);
            }
            else if (dialogResult.CommandType == ExchangeDialogCommand.DisableArticle)
            {
                await DisableArticle(node);
            }
        }
        private async Task EnableArticle(TreeNodeModel node)
        {
            if (node.Tag is not ScriptModel script)
            {
                return;
            }

            if (!node.Title.StartsWith('_'))
            {
                return;
            }

            string newName = node.Title.TrimStart('_');

            bool success = await ChangeScriptTitle(new ScriptModel()
            {
                Uuid = script.Uuid,
                Name = newName
            });

            if (success)
            {
                node.Title = newName;
                script.Name = newName;
            }
            else
            {
                Navigator.NavigateTo("/error-page");
            }
        }
        private async Task DisableArticle(TreeNodeModel node)
        {
            if (node.Tag is not ScriptModel script)
            {
                return;
            }

            if (node.Title.StartsWith('_'))
            {
                return;
            }

            string newName = $"_{node.Title}";

            bool success = await ChangeScriptTitle(new ScriptModel()
            {
                Uuid = script.Uuid,
                Name = newName
            });

            if (success)
            {
                node.Title = newName;
                script.Name = newName;
            }
            else
            {
                Navigator.NavigateTo("/error-page");
            }
        }
        private async Task<bool> ChangeScriptTitle(ScriptModel script)
        {
            try
            {
                HttpResponseMessage response = await Http.PutAsJsonAsync($"/api/name", script);

                if (response.StatusCode == HttpStatusCode.OK) { return true; }

                AppState.LastErrorText = response.ReasonPhrase;
            }
            catch (Exception error)
            {
                AppState.LastErrorText = error.Message;
            }
            return false;
        }
        private async Task DeleteArticle(TreeNodeModel node)
        {
            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseModel>(in node);

            if (root is null || root.Tag is not InfoBaseModel database)
            {
                return; // owner database is not found
            }

            if (node.Parent.Tag is not ScriptModel parent)
            {
                return;
            }

            if (node.Tag is not ScriptModel child)
            {
                return;
            }

            bool success = await DeleteArticle(database.Name, node.Parent.Parent.Parent.Title, node.Parent.Title, node.Title);

            if (!success)
            {
                Navigator.NavigateTo("/error-page"); return;
            }

            parent.Children.Remove(child);

            node.Parent.Nodes.Remove(node);

            node.IsVisible = false;
        }
        private async Task<bool> DeleteArticle(string database, string publication, string type, string article)
        {
            try
            {
                string url = $"/exchange/{database}/{publication}/{type}/{article}";

                HttpResponseMessage response = await Http.DeleteAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                AppState.LastErrorText = response.ReasonPhrase;
            }
            catch (Exception error)
            {
                AppState.LastErrorText = error.Message;
            }
            return false;
        }

        private async Task OpenScriptContextMenu(TreeNodeModel node, IDialogService dialogService)
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
            var dialog = dialogService.Show<ScriptDialog>(node.Url, parameters, options);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }

            if (result.Data is not ExchangeDialogResult dialogResult)
            {
                return;
            }

            if (dialogResult.CommandType == ExchangeDialogCommand.EnableScript)
            {
                await EnableScript(node);
            }
            else if (dialogResult.CommandType == ExchangeDialogCommand.DisableScript)
            {
                await DisableScript(node);
            }
            else if (dialogResult.CommandType == ExchangeDialogCommand.OpenScriptInEditor)
            {
                if (node.Tag is ScriptModel script && !script.IsFolder)
                {
                    Navigator.NavigateTo($"/script-editor/{script.Uuid}");
                }
            }
        }
        private async Task EnableScript(TreeNodeModel node)
        {
            if (node.Tag is not ScriptModel script)
            {
                return;
            }

            if (!node.Title.StartsWith('_'))
            {
                return;
            }

            string newName = node.Title.TrimStart('_');

            bool success = await ChangeScriptTitle(new ScriptModel()
            {
                Uuid = script.Uuid,
                Name = newName
            });

            if (success)
            {
                node.Title = newName;
                script.Name = newName;
            }
            else
            {
                Navigator.NavigateTo("/error-page");
            }
        }
        private async Task DisableScript(TreeNodeModel node)
        {
            if (node.Tag is not ScriptModel script)
            {
                return;
            }

            if (node.Title.StartsWith('_'))
            {
                return;
            }

            string newName = $"_{node.Title}";

            bool success = await ChangeScriptTitle(new ScriptModel()
            {
                Uuid = script.Uuid,
                Name = newName
            });

            if (success)
            {
                node.Title = newName;
                script.Name = newName;
            }
            else
            {
                Navigator.NavigateTo("/error-page");
            }
        }
    }
}