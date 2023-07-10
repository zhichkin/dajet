using MudBlazor;

namespace DaJet.Studio.Pages
{
    public enum ExchangeDialogCommand
    {
        SelectExchange, DeleteExchange, CreatePipeline, CreateArticle, DeleteArticle, EnableArticle, DisableArticle,
        EnableScript, DisableScript, OpenScriptInEditor, ConfigureRabbitMQ
    }
    public sealed class ExchangeDialogResult : DialogResult
    {
        public ExchangeDialogResult(object data, Type resultType, bool cancelled) : base(data, resultType, cancelled) { }
        public string ArticleName { get; set; } = string.Empty;
        public ExchangeDialogCommand CommandType { get; set; } = ExchangeDialogCommand.SelectExchange;
    }
}