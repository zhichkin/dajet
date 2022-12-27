namespace DaJet.Studio.Pages
{
    public sealed class TreeNodeModel
    {
        public object Model { get; set; }
        public string Icon { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool CanExpand { get; set; } = true;
        public HashSet<TreeNodeModel> Nodes { get; set; } = new();
        public override int GetHashCode()
        {
            return Title.GetHashCode();
        }
        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Title) ? "Noname" : Title;
        }
    }
}