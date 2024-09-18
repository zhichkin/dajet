using DaJet.Http.Client;
using DaJet.Model;
using DaJet.Studio.Components;
using Microsoft.AspNetCore.Components;
using System.ComponentModel;

namespace DaJet.Studio.Controllers
{
    public sealed class DaJetCodeController
    {
        private readonly DaJetHttpClient DaJetClient;
        private readonly NavigationManager Navigator;
        public Func<TreeNodeModel, ElementReference, Task> OpenContextMenuHandler { get; set; }
        public DaJetCodeController(DaJetHttpClient client, NavigationManager navigator)
        {
            DaJetClient = client ?? throw new ArgumentNullException(nameof(client));
            Navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        }
        public void NavigateToServerLogPage() { Navigator.NavigateTo("/dajet-server-log"); }
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
                Title = "code",
                OpenNodeHandler = OpenCodeNodeHandler,
                ContextMenuHandler = ShowContextMenu,
                DropDataHandler = DropDataHandler,
                CanAcceptDropData = CanAcceptDropData
            };
        }
        public void ConfigureCodeItemNode(in TreeNodeModel node, in CodeItem model)
        {
            node.Tag = model;
            node.Title = model.Name;
            node.Url = $"{node.Parent.Url}/{model.Name}";
            node.Icon = model.IsFolder ? "/img/folder-closed.png" : "/img/script.png";
            node.UseToggle = model.IsFolder;
            node.CanBeEdited = true;
            node.IsDraggable = true;
            node.OpenNodeHandler = OpenCodeNodeHandler;
            node.NodeClickHandler = CodeItemClickHandler;
            node.UpdateTitleCommand = UpdateNodeTitleHandler;
            node.ContextMenuHandler = ShowContextMenu;
            node.DropDataHandler = DropDataHandler;
            node.CanAcceptDropData = CanAcceptDropData;
        }
        public async Task OpenCodeNodeHandler(TreeNodeModel parent)
        {
            if (parent is null) { return; }

            try
            {
                parent.Nodes.Clear();

                List<CodeItem> list = await DaJetClient.GetFolderItems(parent.Url);

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
            string result = string.Empty;

            try
            {
                if (model.IsFolder)
                {
                    result = await DaJetClient.RenameScriptFolder(node.Url, node.Title);
                }
                else
                {
                    result = await DaJetClient.RenameScriptFile(node.Url, node.Title);
                }

                success = string.IsNullOrEmpty(result);
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
        public bool CanAcceptDropData(TreeNodeModel source, TreeNodeModel target)
        {
            if (source is null || target is null) { return false; }

            if (target.HasAncestor(source)) { return false; }

            if (target.Nodes.Contains(source)) { return false; }

            if (target.Tag is CodeItem item && !item.IsFolder) { return false; }

            return true;
        }
        public async Task DropDataHandler(TreeNodeModel source, TreeNodeModel target)
        {
            if (!CanAcceptDropData(source, target)) { return; }

            TreeNodeModel parent = source.Parent;

            if (source.Tag is CodeItem item)
            {
                string result = string.Empty;
                
                if (item.IsFolder)
                {
                    result = await DaJetClient.MoveScriptFolder(source.Url, target.Url);
                }
                else
                {
                    result = await DaJetClient.MoveScriptFile(source.Url, target.Url);
                }

                if (string.IsNullOrEmpty(result))
                {
                    source.Parent = target;
                    target.Nodes.Add(source);

                    if (item.IsFolder)
                    {
                        RebuildUrlsRecursively(in source);
                    }
                    else
                    {
                        source.Url = $"{target.Url}/{item.Name}";
                    }

                    if (parent is not null)
                    {
                        parent.Nodes.Remove(source);
                        parent.NotifyStateChanged();
                    }
                }
            }
        }
        private static void RebuildUrlsRecursively(in TreeNodeModel node)
        {
            if (node.Parent is not null)
            {
                node.Url = $"{node.Parent.Url}/{node.Title}";
            }

            foreach (TreeNodeModel child in node.Nodes)
            {
                RebuildUrlsRecursively(in child);
            }
        }
    }
}