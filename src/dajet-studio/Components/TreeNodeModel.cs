﻿using MudBlazor;

namespace DaJet.Studio.Components
{
    public sealed class TreeNodeModel
    {
        public object Tag { get; set; } = null;
        public string Url { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public bool UseToggle { get; set; } = true;
        public bool IsExpanded { get; set; } = false;
        public TreeNodeModel Parent { get; set; }
        public List<TreeNodeModel> Nodes { get; set; } = new();
        public Func<Task> ToggleCommand { get; private set; }
        public Func<IDialogService, Task> ContextMenuCommand { get; private set; }
        public Func<TreeNodeModel, Task> OpenNodeHandler { get; set; }
        public Func<TreeNodeModel, IDialogService, Task> ContextMenuHandler { get; set; }
        public TreeNodeModel()
        {
            ToggleCommand = new(ToggleCommandHandler);
            ContextMenuCommand = new(ContextMenuCommandHandler);
        }
        public static TreeNodeModel GetAncestor<T>(in TreeNodeModel node)
        {
            TreeNodeModel parent = node;

            while (parent != null)
            {
                if (parent.Tag is T)
                {
                    return parent;
                }

                parent = parent.Parent;
            }

            return null;
        }
        private async Task ToggleCommandHandler()
        {
            IsExpanded = !IsExpanded;

            if (IsExpanded && OpenNodeHandler != null)
            {
                await OpenNodeHandler(this);
            }
        }
        private async Task ContextMenuCommandHandler(IDialogService dialogService)
        {
            if (ContextMenuHandler != null)
            {
                await ContextMenuHandler(this, dialogService);
            }
        }
        public override string ToString()
        {
            return Title;
        }
    }
}