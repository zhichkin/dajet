using DaJet.Http.Client;
using DaJet.Model;
using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using System.ComponentModel;

namespace DaJet.Studio.Controllers
{
    public sealed class DaJetCodeController
    {
        private readonly AppState AppState;
        private readonly DaJetHttpClient DaJetClient;
        private readonly NavigationManager Navigator;
        public Func<TreeNodeModel, ElementReference, Task> OpenContextMenuHandler { get; set; }
        public DaJetCodeController(AppState state, DaJetHttpClient client, NavigationManager navigator)
        {
            AppState = state ?? throw new ArgumentNullException(nameof(state));
            DaJetClient = client ?? throw new ArgumentNullException(nameof(client));
            Navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        }
        public Task ShowContextMenu(TreeNodeModel node, ElementReference source)
        {
            if (node.Tag is not CodeItem)
            {
                return Task.CompletedTask;
            }

            OpenContextMenuHandler?.Invoke(node, source);

            return Task.CompletedTask;
        }
        public TreeNodeModel CreateRootNode()
        {
            return new TreeNodeModel()
            {
                Tag = new CodeItem()
                {
                    Name = "code",
                    IsFolder = true
                },
                Url = "/code",
                Title = "code",
                OpenNodeHandler = OpenCodeNodeHandler,
                ContextMenuHandler = ShowContextMenu
            };
        }
        public void ConfigureCodeItemNode(in TreeNodeModel node, in CodeItem model)
        {
            node.Tag = model;
            node.Title = model.Name;
            node.Url = $"{node.Parent.Url}/{model.Name}";
            node.Icon = model.IsFolder ? (node.IsExpanded ? "/img/folder-opened.png" : "/img/folder-closed.png") : "/img/script.png";
            node.UseToggle = model.IsFolder;
            node.CanBeEdited = true;
            node.OpenNodeHandler = OpenCodeNodeHandler;
            node.NodeClickHandler = CodeItemClickHandler;
            node.UpdateTitleCommand = UpdateNodeTitleHandler;
            node.ContextMenuHandler = ShowContextMenu;
        }
        public async Task OpenCodeNodeHandler(TreeNodeModel parent)
        {
            if (parent is null) { return; }

            try
            {
                parent.Nodes.Clear();

                List<CodeItem> list = await DaJetClient.GetCodeItems(parent.Url);

                foreach (CodeItem item in list)
                {
                    TreeNodeModel child = new()
                    {
                        Parent = parent
                    };

                    ConfigureCodeItemNode(in child, in item);

                    parent.Nodes.Add(child);
                }
            }
            catch
            {
                parent.Nodes.Add(new TreeNodeModel()
                {
                    UseToggle = false,
                    Title = "Ошибка загрузки данных!"
                });
            }
        }
        public Task CodeItemClickHandler(TreeNodeModel node)
        {
            if (node is null || node.Tag is not CodeItem item || item.IsFolder)
            {
                return Task.CompletedTask;
            }

            try
            {
                Navigator.NavigateTo($"/dajet-code-editor{node.Url}");
            }
            catch (Exception error)
            {
                //Snackbar.Add(error.Message, Severity.Error);
            }

            return Task.CompletedTask;
        }
        public async Task UpdateNodeTitleHandler(TreeNodeModel node, CancelEventArgs args)
        {
            if (node.Tag is not CodeItem model)
            {
                return;
            }

            bool success = true;

            try
            {
                await DaJetClient.RenameScriptFile(node.Url, node.Title);
            }
            catch
            {
                success = false;
            }

            if (success)
            {
                model.Name = node.Title; // commit edit title operation
                node.Url = $"{node.Parent.Url}/{model.Name}";
            }
            else
            {
                args.Cancel = true; // rollback edit title operation
            }
        }
    }
}