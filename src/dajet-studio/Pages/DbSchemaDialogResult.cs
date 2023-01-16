using MudBlazor;

namespace DaJet.Studio.Pages
{
    public enum DbSchemaDialogCommand { Script, Create, Update, Delete }
    public sealed class DbSchemaDialogResult : DialogResult
    {
        public DbSchemaDialogResult(object data, Type resultType, bool cancelled) : base(data, resultType, cancelled) { }
        public DbSchemaDialogCommand CommandType { get; set; } = DbSchemaDialogCommand.Create;
    }
}