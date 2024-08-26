using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Code
{
    public partial class DaJetCodeEditor : ComponentBase, IDisposable
    {
        [Parameter] public string FilePath { get; set; }
        protected string SourceCode { get; set; } = string.Empty;
        protected bool ScriptIsChanged { get; set; } = false;
        protected string ErrorText { get; set; } = string.Empty;
        protected string ResultText { get; set; } = string.Empty;
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override async Task OnParametersSetAsync()
        {
            if (!string.IsNullOrEmpty(FilePath))
            {
                SourceCode = await DaJetClient.GetSourceCode($"/{FilePath}");
            }
        }
        protected void OnScriptChanged(ChangeEventArgs args)
        {
            ScriptIsChanged = true;
            SourceCode = args.Value.ToString();
        }
        public void Dispose()
        {
            //NOTE: check for unsaved changes here
        }
        private async Task SaveSourceCode()
        {
            try
            {
                string result = await DaJetClient.SaveSourceCode($"/{FilePath}", SourceCode);

                if (string.IsNullOrEmpty(result))
                {
                    ScriptIsChanged = false;
                }
            }
            catch (Exception error)
            {
                ErrorText = error.Message;
            }
        }
        private async Task ExecuteScript()
        {
            ErrorText = string.Empty;
            ResultText = string.Empty;

            string result = await DaJetClient.ExecuteScript($"/{FilePath}", SourceCode);

            //if (result.Success)
            //{
            //    ResultText = result.Script;
            //}
            //else
            //{
            //    ErrorText = result.Error;
            //}
        }
    }
}