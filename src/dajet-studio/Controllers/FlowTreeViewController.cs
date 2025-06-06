﻿using DaJet.Http.Client;
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
        private IJSRuntime JS { get; set; }
        private IDomainModel DomainModel { get; set; }
        private DaJetHttpClient DataSource { get; set; }
        private NavigationManager Navigator { get; set; }
        private IDialogService DialogService { get; set; }
        public FlowTreeViewController(IDomainModel domain, DaJetHttpClient client, NavigationManager navigator, IJSRuntime js, IDialogService dialogService)
        {
            JS = js;
            DomainModel = domain;
            DataSource = client;
            Navigator = navigator;
            DialogService = dialogService;
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
        private async Task ContextMenuHandler(TreeNodeModel node, ElementReference element)
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
            var dialog = DialogService.Show<FlowTreeNodeDialog>(node.Title, parameters, options);
            var result = await dialog.Result;
            if (result.Canceled) { return; }

            if (result.Data is not TreeNodeDialogResult dialogResult)
            {
                return;
            }

            if (dialogResult.CommandType == TreeNodeDialogCommand.SelectFolder)
            {
                await RefreshFolder(node);
            }
            else if (dialogResult.CommandType == TreeNodeDialogCommand.CreateFolder)
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
                await DeletePipeline(node);
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
                Navigator.NavigateTo($"/flow/table");
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

        private async Task RefreshFolder(TreeNodeModel node)
        {
            if (node.Tag is not TreeNodeRecord parent)
            {
                return;
            }

            var list = await DataSource.QueryAsync<TreeNodeRecord>(parent.GetEntity());

            node.Nodes.Clear();

            foreach (var item in list)
            {
                CreateTreeNode(in node, in item);
            }
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
            string message = $"Удалить каталог \"{node.Title}\" ?";

            bool confirmed = await JS.InvokeAsync<bool>("confirm", message);

            if (!confirmed) { return; }

            await DeleteRecursively(node);

            node.Parent.Nodes.Remove(node);

            node.IsVisible = false;
        }
        private async Task DeleteTreeNode(TreeNodeModel node)
        {
            if (node.Tag is not TreeNodeRecord treeNode)
            {
                return;
            }

            if (treeNode.Value.Identity != Guid.Empty)
            {
                int typeCode = DomainModel.GetTypeCode(typeof(PipelineRecord));

                await DataSource.DeleteAsync(treeNode.Value);

                if (treeNode.Value.TypeCode == typeCode)
                {
                    await DataSource.DeletePipeline(treeNode.Value.Identity);
                }
            }

            await DataSource.DeleteAsync(treeNode.GetEntity());
        }
        private async Task DeleteRecursively(TreeNodeModel parent)
        {
            foreach (TreeNodeModel child in parent.Nodes)
            {
                await DeleteRecursively(child);
            }
            await DeleteTreeNode(parent);
        }

        private async Task CreatePipeline(TreeNodeModel node)
        {
            if (node.Tag is not TreeNodeRecord parent || !parent.IsFolder)
            {
                return;
            }

            string name = "new-pipeline";
            TreeNodeRecord treeNode = DomainModel.New<TreeNodeRecord>();
            PipelineRecord pipeline = DomainModel.New<PipelineRecord>();

            pipeline.Name = name;
            pipeline.Activation = ActivationMode.Manual;

            treeNode.Name = name;
            treeNode.Value = pipeline.GetEntity();
            treeNode.Parent = parent.GetEntity();
            treeNode.IsFolder = false;

            try
            {
                await DataSource.CreateAsync(pipeline);
                await CreatePipelineOptions(pipeline.GetEntity());
                await DataSource.CreateAsync(treeNode);

                CreateTreeNode(in node, treeNode);
            }
            catch
            {
                throw;
            }
        }
        private async Task CreatePipelineOptions(Entity pipeline)
        {
            OptionRecord option = DomainModel.New<OptionRecord>();
            option.Owner = pipeline;
            option.Name = "SleepTimeout";
            option.Type = "System.Int32";
            option.Value = "0";

            await DataSource.CreateAsync(option);

            option = DomainModel.New<OptionRecord>();
            option.Owner = pipeline;
            option.Name = "ShowStackTrace";
            option.Type = "System.Boolean";
            option.Value = "false";

            await DataSource.CreateAsync(option);
        }
        private async Task DeletePipeline(TreeNodeModel node)
        {
            string message = $"Удалить узел \"{node.Title}\" ?";

            bool confirmed = await JS.InvokeAsync<bool>("confirm", message);

            if (!confirmed) { return; }

            if (node.Tag is not TreeNodeRecord treeNode)
            {
                return;
            }

            int typeCode = DomainModel.GetTypeCode(typeof(PipelineRecord));

            try
            {
                if (treeNode.Value.Identity != Guid.Empty)
                {
                    await DataSource.DeleteAsync(treeNode.Value);

                    if (treeNode.Value.TypeCode == typeCode)
                    {
                        await DataSource.DeletePipeline(treeNode.Value.Identity);
                    }
                }

                await DataSource.DeleteAsync(treeNode.GetEntity());

                node.Parent.Nodes.Remove(node);

                node.IsVisible = false;
            }
            catch
            {
                throw;
            }
        }
    }
}