using MudBlazor;

namespace DaJet.Studio.Pages
{
    public enum TreeNodeDialogCommand
    {
        CreateFolder, CreateEntity,
        UpdateFolder, UpdateEntity,
        DeleteFolder, DeleteEntity,
        SelectFolder, SelectEntity
    }
    public sealed class TreeNodeDialogResult : DialogResult
    {
        public TreeNodeDialogResult(object data, Type resultType, bool cancelled) : base(data, resultType, cancelled) { }
        public TreeNodeDialogCommand CommandType { get; set; } = TreeNodeDialogCommand.CreateFolder;
    }
}