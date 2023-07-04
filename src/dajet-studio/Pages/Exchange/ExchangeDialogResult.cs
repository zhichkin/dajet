using MudBlazor;

namespace DaJet.Studio.Pages
{
    public enum ExchangeDialogCommand
    {
        SelectExchange, DeleteExchange, CreatePipeline
    }
    public sealed class ExchangeDialogResult : DialogResult
    {
        public ExchangeDialogResult(object data, Type resultType, bool cancelled) : base(data, resultType, cancelled) { }
        public ExchangeDialogCommand CommandType { get; set; } = ExchangeDialogCommand.SelectExchange;
    }
}