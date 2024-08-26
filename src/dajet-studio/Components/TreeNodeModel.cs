using Microsoft.AspNetCore.Components;
using System.ComponentModel;

namespace DaJet.Studio.Components
{
    public sealed class TreeNodeModel
    {
        public static TreeNodeModel GetRootNode(in TreeNodeModel node)
        {
            TreeNodeModel root = node;

            while (root.Parent is not null)
            {
                root = root.Parent;
            }

            return root;
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
        public bool HasAncestor(in TreeNodeModel node)
        {
            TreeNodeModel parent = Parent;

            while (parent is not null)
            {
                if (parent == node)
                {
                    return true;
                }

                parent = parent.Parent;
            }

            return false;
        }
        public TreeNodeModel()
        {
            ToggleCommand = new(ToggleCommandHandler);
            NodeClickCommand = new(NodeClickCommandHandler);
            ContextMenuCommand = new(ContextMenuCommandHandler);
        }
        public event Action StateHasChanged;
        public void NotifyStateChanged() { StateHasChanged?.Invoke(); }
        public object Tag { get; set; } = null;
        public string Url { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public bool UseToggle { get; set; } = true;
        public bool IsExpanded { get; set; } = false;
        public bool IsDraggable { get; set; } = false;
        public bool CanBeEdited { get; set; } = false;
        public bool IsInEditMode { get; set; } = false;
        public TreeNodeModel Parent { get; set; }
        public List<TreeNodeModel> Nodes { get; set; } = new();
        public Func<Task> ToggleCommand { get; private set; }
        public Func<Task> NodeClickCommand { get; private set; }
        public Func<ElementReference, Task> ContextMenuCommand { get; private set; }
        public Func<TreeNodeModel, TreeNodeModel, Task> DropDataHandler { get; set; }
        public Func<TreeNodeModel, TreeNodeModel, bool> CanAcceptDropData { get; set; }
        public Func<TreeNodeModel, Task> OpenNodeHandler { get; set; }
        public Func<TreeNodeModel, Task> NodeClickHandler { get; set; }
        public Func<TreeNodeModel, CancelEventArgs, Task> UpdateTitleCommand { get; set; }
        public Func<TreeNodeModel, ElementReference, Task> ContextMenuHandler { get; set; }
        private async Task ToggleCommandHandler()
        {
            IsExpanded = !IsExpanded;

            if (IsExpanded && OpenNodeHandler != null)
            {
                await OpenNodeHandler(this);
            }
        }
        private async Task NodeClickCommandHandler()
        {
            if (NodeClickHandler is not null)
            {
                await NodeClickHandler(this);
            }
        }
        private async Task ContextMenuCommandHandler(ElementReference element)
        {
            if (ContextMenuHandler is not null)
            {
                await ContextMenuHandler(this, element);
            }
        }
        public override string ToString()
        {
            return Title;
        }
    }
}