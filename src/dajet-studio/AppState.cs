namespace DaJet.Studio
{
    public sealed class AppState
    {
        public Action RefreshInfoBaseCommand;
        public string CurrentInfoBase { get; set; } = string.Empty;
        public string FooterText { get; set; } = string.Empty;
    }
}