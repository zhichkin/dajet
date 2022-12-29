namespace DaJet.Studio
{
    public sealed class AppState
    {
        public event Action OnRefreshMainTreeView;
        public void RefreshMainTreeView()
        {
            OnRefreshMainTreeView?.Invoke();
        }
    }
}