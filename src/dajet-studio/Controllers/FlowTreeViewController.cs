using DaJet.Http.Client;
using DaJet.Model;
using DaJet.Studio.Components;
using DaJet.Studio.Pages;
using DaJet.Studio.Pages.Flow;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using System.ComponentModel;

namespace DaJet.Studio.Controllers
{
    public sealed class FlowTreeViewController
    {
        private IDomainModel DomainModel { get; set; }
        private DaJetHttpClient DataSource { get; set; }
        private IJSRuntime JSRuntime { get; set; }
        private NavigationManager Navigator { get; set; }
        public FlowTreeViewController(IDomainModel domain, DaJetHttpClient client, IJSRuntime js, NavigationManager navigator)
        {
            DomainModel = domain;
            DataSource = client;
            JSRuntime = js;
            Navigator = navigator;
        }
        public TreeNodeModel CreateRootNode(TreeNodeRecord root)
        {
            return new TreeNodeModel()
            {
                Tag = root,
                Title = "flow",
                OpenNodeHandler = OpenNodeHandler,
                ContextMenuHandler = ContextMenuHandler,
                DropDataHandler = DropDataHandler,
                CanAcceptDropData = CanAcceptDropData
            };
        }
        private async Task OpenNodeHandler(TreeNodeModel parent)
        {
            if (parent.Tag is not TreeNodeRecord node)
            {
                return;
            }

            if (parent is null || parent.Nodes.Count > 0)
            {
                return;
            }

            var list = await DataSource.QueryAsync<TreeNodeRecord>(node.GetEntity());

            foreach (var item in list)
            {
                CreateTreeNode(in parent, in item);
            }
        }
        private void CreateTreeNode(in TreeNodeModel parent, in TreeNodeRecord node)
        {
            TreeNodeModel child = new()
            {
                Tag = node,
                Parent = parent,
                Title = node.Name,
                UseToggle = node.IsFolder,
                CanBeEdited = true,
                IsDraggable = true,
                OpenNodeHandler = node.IsFolder ? OpenNodeHandler : null,
                ContextMenuHandler = ContextMenuHandler,
                UpdateTitleCommand = UpdateTitleHandler,
                DropDataHandler = DropDataHandler,
                CanAcceptDropData = CanAcceptDropData
            };

            parent.Nodes.Add(child);
        }
        private async Task UpdateTitleHandler(TreeNodeModel node, CancelEventArgs args)
        {
            if (node.Tag is not TreeNodeRecord record)
            {
                return;
            }

            string name = record.Name;

            try
            {
                record.Name = node.Title;

                if (record.IsChanged())
                {
                    await DataSource.UpdateAsync(record);
                }
            }
            catch
            {
                args.Cancel = true;
                record.Name = name;
            }
        }
        private async Task ContextMenuHandler(TreeNodeModel node, IDialogService dialogService)
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
            var dialog = dialogService.Show<FlowTreeNodeDialog>(node.Title, parameters, options);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }

            if (result.Data is not TreeNodeDialogResult dialogResult)
            {
                return;
            }

            if (dialogResult.CommandType == TreeNodeDialogCommand.CreateFolder)
            {
                await CreateFolder(node);
            }
            else if (dialogResult.CommandType == TreeNodeDialogCommand.UpdateFolder)
            {
                NavigateToPipelineTable(node);
            }
            else if (dialogResult.CommandType == TreeNodeDialogCommand.DeleteFolder)
            {
                await DeleteFolder(node);
            }
            else if (dialogResult.CommandType == TreeNodeDialogCommand.CreateEntity)
            {
                await CreatePipeline(node);
            }
            else if (dialogResult.CommandType == TreeNodeDialogCommand.UpdateEntity)
            {
                NavigateToPipelinePage(node);
            }
            else if (dialogResult.CommandType == TreeNodeDialogCommand.DeleteEntity)
            {
                //await DeleteFolderScript(node);
            }
        }
        private bool CanAcceptDropData(TreeNodeModel source, TreeNodeModel target)
        {
            if (source is null || target is null) { return false; }

            if (target.HasAncestor(source)) { return false; }

            if (target.Nodes.Contains(source)) { return false; }

            if (target.Tag is TreeNodeRecord record && !record.IsFolder) { return false; }

            return true;
        }
        private async Task DropDataHandler(TreeNodeModel source, TreeNodeModel target)
        {
            if (!CanAcceptDropData(source, target)) { return; }

            TreeNodeModel parent = source.Parent;

            if (source.Tag is TreeNodeRecord record && target.Tag is TreeNodeRecord owner)
            {
                record.Parent = owner.GetEntity();

                if (record.IsChanged())
                {
                    await DataSource.UpdateAsync(record);

                    source.Parent = target;
                    target.Nodes.Add(source);

                    if (parent is not null)
                    {
                        parent.Nodes.Remove(source);
                        parent.NotifyStateChanged();
                    }
                }
            }
        }

        private void NavigateToPipelineTable(TreeNodeModel node)
        {
            if (node is null) { return; }

            if (node.Parent is null)
            {
                Navigator.NavigateTo($"/dajet-flow");
            }
            else if (node.Tag is TreeNodeRecord record)
            {
                Navigator.NavigateTo($"/flow/table/{record.Identity.ToString().ToLower()}");
            }
        }
        private void NavigateToPipelinePage(TreeNodeModel node)
        {
            if (node is null || node.Tag is not TreeNodeRecord record || record.IsFolder)
            {
                return;
            }

            Navigator.NavigateTo($"/flow/pipeline/{record.Identity.ToString().ToLower()}");
        }

        private async Task CreateFolder(TreeNodeModel node)
        {
            if (node.Tag is not TreeNodeRecord parent)
            {
                return;
            }

            TreeNodeRecord record = DomainModel.New<TreeNodeRecord>();

            record.Parent = parent.GetEntity();
            record.Name = "New folder";
            record.IsFolder = true;
            record.Value = Entity.Undefined;

            try
            {
                await DataSource.CreateAsync(record);

                CreateTreeNode(in node, record);
            }
            catch
            {
                throw;
            }
        }
        private async Task DeleteFolder(TreeNodeModel node)
        {
            if (node.Tag is not TreeNodeRecord record)
            {
                return;
            }

            try
            {
                await DataSource.DeleteAsync(record.GetEntity());

                node.Parent.Nodes.Remove(node);

                node.IsVisible = false;
            }
            catch
            {
                throw;
            }
        }

        private async Task CreatePipeline(TreeNodeModel node)
        {
            if (node.Tag is not TreeNodeRecord parent || !parent.IsFolder)
            {
                return;
            }

            string name = "New pipeline";
            TreeNodeRecord treeNode = DomainModel.New<TreeNodeRecord>();
            PipelineRecord pipeline = DomainModel.New<PipelineRecord>();

            pipeline.Name = name;
            pipeline.Activation = PipelineMode.Manual;

            treeNode.Name = name;
            treeNode.Value = pipeline.GetEntity();
            treeNode.Parent = parent.GetEntity();
            treeNode.IsFolder = false;

            try
            {
                await DataSource.CreateAsync(pipeline);
                await DataSource.CreateAsync(treeNode);

                CreateTreeNode(in node, treeNode);
            }
            catch
            {
                throw;
            }
        }
    }
}