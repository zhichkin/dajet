namespace DaJet.Studio
{
    public sealed class AppState
    {
        public Action RefreshInfoBaseCommand;
        public string CurrentInfoBase { get; set; } = string.Empty;
    }
}