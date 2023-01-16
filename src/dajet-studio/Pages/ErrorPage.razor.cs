using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages
{
    public partial class ErrorPage : ComponentBase, IDisposable
    {
        public string ErrorText { get; set; }
        protected override void OnInitialized()
        {
            ErrorText = AppState.LastErrorText;
            AppState.AppErrorEventHandler += HandleError;
        }
        public void Dispose()
        {
            AppState.AppErrorEventHandler -= HandleError;
        }
        private void ClosePage()
        {
            Navigator.NavigateTo("/");
        }
        private void HandleError(string error)
        {
            ErrorText = error;
            StateHasChanged();
        }
    }
}