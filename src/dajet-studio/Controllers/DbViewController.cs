using DaJet.Model;
using DaJet.Studio.Components;
using DaJet.Studio.Model;
using DaJet.Studio.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using System.Net;
using System.Net.Http.Json;

namespace DaJet.Studio.Controllers
{
    public sealed class CreateViewsRequest
    {
        public string Schema { get; set; } = string.Empty;
        public bool Codify { get; set; } = false;
    }
    public sealed class CreateViewsResponse
    {
        public int Result { get; set; }
        public List<string> Errors { get; set; } = new();
    }
    public sealed class DbViewController
    {
        private HttpClient Http { get; set; }
        private IJSRuntime JSRuntime { get; set; }
        private AppState AppState { get; set; }
        private NavigationManager Navigator { get; set; }
        public DbViewController(HttpClient http, IJSRuntime js, AppState state, NavigationManager navigator)
        {
            Http = http;
            JSRuntime = js;
            AppState = state;
            Navigator = navigator;
        }
        public TreeNodeModel CreateRootNode(InfoBaseRecord model)
        {
            return new TreeNodeModel()
            {
                Tag = model,
                Url = $"/db/schema/{model.Name}",
                Title = "dbv",
                OpenNodeHandler = OpenDbViewNodeHandler,
                ContextMenuHandler = DbViewContextMenuHandler
            };
        }
        private TreeNodeModel CreateSchemaNode(TreeNodeModel parent, string schema)
        {
            TreeNodeModel node = new()
            {
                Parent = parent,
                Tag = schema,
                Url = $"{parent.Url}/{schema}",
                Title = schema,
                OpenNodeHandler = OpenDbSchemaNodeHandler,
                ContextMenuHandler = DbSchemaContextMenuHandler
            };

            return node;
        }
        private void RemoveSchemaNode(TreeNodeModel parent, string schema)
        {
            for (int i = 0; i < parent.Nodes.Count; i++)
            {
                TreeNodeModel node = parent.Nodes[i];

                if (node.Title == schema)
                {
                    parent.Nodes.RemoveAt(i);
                    break;
                }
            }
        }
        private async Task RefreshSchemaNode(TreeNodeModel node)
        {
            node.Nodes.Clear();
            await OpenDbSchemaNodeHandler(node);
        }
        private async Task OpenDbViewNodeHandler(TreeNodeModel root)
        {
            if (root == null || root.Nodes.Count > 0)
            {
                return;
            }

            if (root.Tag is not InfoBaseRecord database)
            {
                return;
            }

            HttpResponseMessage response = await Http.GetAsync(root.Url);

            List<string> list = await response.Content.ReadFromJsonAsync<List<string>>();

            foreach (string schema in list)
            {
                root.Nodes.Add(CreateSchemaNode(root, schema));
            }
        }
        private async Task DbViewContextMenuHandler(TreeNodeModel node, IDialogService dialogService)
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
            var dialog = dialogService.Show<DbViewDialog>(node.Url, parameters, options);
            var result = await dialog.Result;
            if (result.Canceled) { return; }

            if (result.Data is not DbViewDialogResult dialogResult)
            {
                return;
            }

            bool success = false;

            if (dialogResult.CommandType == DbViewDialogCommand.Create)
            {
                success = await CreateSchema($"{node.Url}/{dialogResult.SchemaName}");

                if (success)
                {
                    node.Nodes.Add(CreateSchemaNode(node, dialogResult.SchemaName));
                }
            }
            else if (dialogResult.CommandType == DbViewDialogCommand.Update)
            {
                node.Nodes.Clear();
                await OpenDbViewNodeHandler(node);
                success = true;
            }
            else if (dialogResult.CommandType == DbViewDialogCommand.Delete)
            {
                success = await DeleteSchema($"{node.Url}/{dialogResult.SchemaName}");

                if (success)
                {
                    RemoveSchemaNode(node, dialogResult.SchemaName);
                }
            }

            if (!success)
            {
                Navigator.NavigateTo("/error-page");
            }
        }
        private async Task OpenDbSchemaNodeHandler(TreeNodeModel node)
        {
            if (node == null || node.Nodes.Count > 0)
            {
                return;
            }

            if (node.Tag is not string schema)
            {
                return;
            }

            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseRecord>(in node);

            if (root == null || root.Tag is not InfoBaseRecord infobase)
            {
                return;
            }

            string url = $"/db/view/select/{infobase.Name}?schema={schema}";

            HttpResponseMessage response = await Http.GetAsync(url);

            List<string> list = await response.Content.ReadFromJsonAsync<List<string>>();

            foreach (string view in list)
            {
                TreeNodeModel child = new()
                {
                    Title = view,
                    Parent = node,
                    UseToggle = false
                };
                node.Nodes.Add(child);
            }
        }
        private async Task DbSchemaContextMenuHandler(TreeNodeModel node, IDialogService dialogService)
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
            var dialog = dialogService.Show<DbSchemaDialog>(node.Url, parameters, options);
            var result = await dialog.Result;
            if (result.Canceled) { return; }

            if (result.Data is not DbSchemaDialogResult dialogResult)
            {
                return;
            }

            bool success = false;

            if (dialogResult.CommandType == DbSchemaDialogCommand.Script)
            {
                await ScriptViewsAndDownload(node);
                return;
            }
            else if (dialogResult.CommandType == DbSchemaDialogCommand.Create)
            {
                success = await CreateViews(node);
            }
            else if (dialogResult.CommandType == DbSchemaDialogCommand.Update)
            {
                success = true;
            }
            else if (dialogResult.CommandType == DbSchemaDialogCommand.Delete)
            {
                success = await DeleteViews(node);
            }

            if (success)
            {
                await RefreshSchemaNode(node);
            }
            else
            {
                Navigator.NavigateTo("/error-page");
            }
        }

        private async Task<bool> CreateSchema(string url)
        {
            try
            {
                HttpResponseMessage response = await Http.PostAsync(url, null);

                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return true;
                }

                AppState.LastErrorText = await response.Content.ReadAsStringAsync();
            }
            catch (Exception error)
            {
                AppState.LastErrorText = error.Message;
            }

            return false;
        }
        private async Task<bool> DeleteSchema(string url)
        {
            try
            {
                HttpResponseMessage response = await Http.DeleteAsync(url);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }

                AppState.LastErrorText = await response.Content.ReadAsStringAsync();
            }
            catch (Exception error)
            {
                AppState.LastErrorText = error.Message;
            }

            return false;
        }
        private async Task ScriptViewsAndDownload(TreeNodeModel node)
        {
            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseRecord>(in node);

            if (root == null || root.Tag is not InfoBaseRecord infobase)
            {
                return;
            }

            if (node.Tag is not string schema)
            {
                return;
            }

            try
            {
                string fileName = $"{infobase.Name}_{schema}.sql";
                string fileUrl = $"{Http.BaseAddress}db/view/{infobase.Name}?schema={schema}";
                await JSRuntime.InvokeVoidAsync("BlazorFileDownload", fileName, fileUrl);
            }
            catch (Exception error)
            {
                AppState.LastErrorText = error.Message;
            }
        }
        private async Task<bool> CreateViews(TreeNodeModel node)
        {
            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseRecord>(in node);

            if (root == null || root.Tag is not InfoBaseRecord infobase)
            {
                return false;
            }

            if (node.Tag is not string schema)
            {
                return false;
            }

            try
            {
                string url = $"/db/view/{infobase.Name}";
                CreateViewsRequest options = new() { Schema = schema };
                HttpResponseMessage response = await Http.PostAsJsonAsync(url, options);
                CreateViewsResponse result = await response.Content.ReadFromJsonAsync<CreateViewsResponse>();
                if (result.Errors != null && result.Errors.Count > 0)
                {
                    AppState.LastErrorText = string.Join(Environment.NewLine, result.Errors);
                    return false;
                }
                return (response.StatusCode == HttpStatusCode.Created);
            }
            catch (Exception error)
            {
                AppState.LastErrorText = error.Message;
            }

            return false;
        }
        private async Task<bool> DeleteViews(TreeNodeModel node)
        {
            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseRecord>(in node);

            if (root == null || root.Tag is not InfoBaseRecord infobase)
            {
                return false;
            }

            if (node.Tag is not string schema)
            {
                return false;
            }

            string url = $"/db/view/{infobase.Name}?schema={schema}";

            try
            {
                HttpResponseMessage response = await Http.DeleteAsync(url);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }

                AppState.LastErrorText = await response.Content.ReadAsStringAsync();
            }
            catch (Exception error)
            {
                AppState.LastErrorText = error.Message;
            }

            return false;
        }
    }
}