using DaJet.Http.Client;
using DaJet.Model;
using DaJet.Studio.Components;
using DaJet.Studio.Pages;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.ComponentModel;

namespace DaJet.Studio.Controllers
{
    public sealed class ApiTreeViewController
    {
        private DaJetHttpClient DataSource { get; set; }
        private NavigationManager Navigator { get; set; }
        private readonly Func<TreeNodeModel, CancelEventArgs, Task> _updateTitleCommandHandler;
        public ApiTreeViewController(DaJetHttpClient client, NavigationManager navigator)
        {
            DataSource = client;
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
                OpenNodeHandler = OpenRootNodeHandler,
                ContextMenuHandler = ContextMenuHandler
            };
        }
        private async Task ContextMenuHandler(TreeNodeModel root, IDialogService dialogService)
        {
            await OpenContextMenu(root, dialogService);
        }
        private async Task OpenRootNodeHandler(TreeNodeModel root)
        {
            if (root == null || root.Nodes.Count > 0)
            {
                return;
            }

            if (root.Tag is not InfoBaseRecord database)
            {
                return;
            }

            IEnumerable<ScriptRecord> list = await DataSource.QueryAsync<ScriptRecord>(database.GetEntity());

            foreach (ScriptRecord model in list)
            {
                if (model.Name == "exchange")
                {
                    continue;
                }

                TreeNodeModel node = await CreateScriptNodeTree(root, model);
                node.Parent = root;
                root.Nodes.Add(node);
            }
        }
        private async Task<TreeNodeModel> CreateScriptNodeTree(TreeNodeModel parent, ScriptRecord model)
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

            IEnumerable<ScriptRecord> children = await DataSource.QueryAsync<ScriptRecord>(model.GetEntity());

            foreach (ScriptRecord script in children)
            {
                TreeNodeModel child = await CreateScriptNodeTree(node, script);
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
            if (result.Canceled) { return; }

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
            ScriptRecord script = DataSource.Model.New<ScriptRecord>();
            script.Name = "NewFolder";
            script.IsFolder = true;

            await CreateFolderScript(node, script);
        }
        private async Task CreateScript(TreeNodeModel node)
        {
            ScriptRecord script = DataSource.Model.New<ScriptRecord>();
            script.Name = "NewScript";
            script.IsFolder = false;

            await CreateFolderScript(node, script);
        }
        private async Task CreateFolderScript(TreeNodeModel node, ScriptRecord script)
        {
            TreeNodeModel root = TreeNodeModel.GetAncestor<InfoBaseRecord>(in node);

            if (root == null || root.Tag is not InfoBaseRecord infobase)
            {
                return; // owner database is not found
            }

            ScriptRecord parent = node.Tag as ScriptRecord;

            if (parent is not null)
            {
                script.Owner = parent.Owner;
                script.Parent = parent.GetEntity();
            }
            else
            {
                script.Owner = infobase.GetEntity();
                script.Parent = Entity.Undefined;
            }

            script.Script = string.Empty;

            bool success = true;

            try
            {
                await DataSource.CreateAsync(script);
            }
            catch
            {
                success = false;
            }

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
                //parent.Children.Add(script);
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
            if (node.Tag is ScriptRecord script)
            {
                Navigator.NavigateTo($"/script-editor/{script.Identity}");
            }
        }
        private async Task UpdateTitleCommandHandler(TreeNodeModel node, CancelEventArgs args)
        {
            if (node.Tag is not ScriptRecord script)
            {
                return;
            }

            bool success = true;

            try
            {
                //TODO: update name only command !? Script property may be changed by script editor !!!

                script = await DataSource.SelectAsync<ScriptRecord>(script.Identity);

                script.Name = node.Title;

                await DataSource.UpdateAsync(script);
                
                node.Tag = script; // commit transaction =)
            }
            catch
            {
                success = false;
            }

            if (success)
            {
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
                args.Cancel = true; // rollback edit title operation
            }
        }
        private async Task DeleteFolderScript(TreeNodeModel node)
        {
            if (node.Tag is not ScriptRecord script)
            {
                return;
            }

            bool success = true;

            try
            {
                await DataSource.DeleteAsync(script.GetEntity());
            }
            catch
            {
                success = false;
            }
            
            if (!success)
            {
                return;
            }

            //if (node.Parent.Tag is ScriptRecord parent && node.Tag is ScriptRecord child)
            //{
            //    parent.Children.Remove(child);
            //}

            node.Parent.Nodes.Remove(node);

            node.IsVisible = false;
        }
    }
}