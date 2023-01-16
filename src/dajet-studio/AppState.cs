namespace DaJet.Studio
{
    public sealed class AppState
    {
        public Action RefreshInfoBaseCommand;
        public string CurrentInfoBase { get; set; } = string.Empty;
        public string FooterText { get; set; } = string.Empty;
        private string _error = string.Empty;
        public string LastErrorText
        {
            get { return _error; }
            set
            {
                _error = value;
                AppErrorEventHandler?.Invoke(_error);
            }
        }
        public event Action<string> AppErrorEventHandler;
    }
}