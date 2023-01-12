using MudBlazor;

namespace DaJet.Studio.Pages
{
    public enum ScriptTreeNodeDialogCommand
    {
        CreateFolder, CreateScript,
        UpdateFolder, UpdateScript,
        DeleteFolder, DeleteScript
    }
    public sealed class ScriptTreeNodeDialogResult : DialogResult
    {
        public ScriptTreeNodeDialogResult(object data, Type resultType, bool cancelled) : base(data, resultType, cancelled) { }
        public ScriptTreeNodeDialogCommand CommandType { get; set; } = ScriptTreeNodeDialogCommand.CreateFolder;
    }
}