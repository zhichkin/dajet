namespace DaJet.Studio.Components
{
    public enum TreeNodeType { Undefined, Catalog, Document, InfoReg, AccumReg }
    public sealed class TreeNodeModel
    {
        public object Tag { get; set; } = null;
        public string Url { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public bool UseToggle { get; set; } = true;
        public bool IsExpanded { get; set; } = false;
        public List<TreeNodeModel> Nodes { get; set; } = new();
        public Func<Task> ToggleCommand { get; private set; }
        public Func<Task> ContextMenuCommand { get; private set; }
        public Func<TreeNodeModel, Task> OpenNodeHandler { get; set; }
        public Func<TreeNodeModel, Task> ContextMenuHandler { get; set; }
        public TreeNodeModel()
        {
            ToggleCommand = new(ToggleCommandHandler);
            ContextMenuCommand = new(ContextMenuCommandHandler);
        }
        private async Task ToggleCommandHandler()
        {
            IsExpanded = !IsExpanded;

            if (IsExpanded && OpenNodeHandler != null)
            {
                await OpenNodeHandler(this);
            }
        }
        private async Task ContextMenuCommandHandler()
        {
            if (ContextMenuHandler != null)
            {
                await ContextMenuHandler(this);
            }
        }
        public override string ToString()
        {
            return Title;
        }
    }
}