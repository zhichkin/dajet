namespace DaJet.Studio.Components
{
    public enum TreeNodeType { Undefined, Catalog, Document, InfoReg, AccumReg }
    public sealed class TreeNodeModel
    {
        public TreeNodeType Type { get; set; } = TreeNodeType.Undefined;
        public object Tag { get; set; } = null;
        public string Url { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsExpanded { get; set; } = false;
        public List<TreeNodeModel> Nodes { get; set; } = new();
        public Func<Task> OpenCloseCommand { get; private set; }
        public Func<TreeNodeModel, Task> OpenNodeHandler { get; set; }
        public TreeNodeModel()
        {
            OpenCloseCommand = new(OpenCloseCommandHandler);
        }
        private async Task OpenCloseCommandHandler()
        {
            IsExpanded = !IsExpanded;

            if (IsExpanded && OpenNodeHandler != null)
            {
                await OpenNodeHandler(this);
            }
        }
        public override string ToString()
        {
            return Title;
        }
    }
}