using MudBlazor;

namespace DaJet.Studio.Pages
{
    public enum DbViewDialogCommand { Create, Update, Delete }
    public sealed class DbViewDialogResult : DialogResult
    {
        public DbViewDialogResult(object data, Type resultType, bool cancelled) : base(data, resultType, cancelled) { }
        public string SchemaName { get; set; } = string.Empty;
        public DbViewDialogCommand CommandType { get; set; } = DbViewDialogCommand.Create;
    }
}