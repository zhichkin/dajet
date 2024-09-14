using DaJet.Data;
using Microsoft.AspNetCore.Components;
using System.Text.Json;

namespace DaJet.Studio.Pages.Code
{
    public partial class DaJetCodeEditor : ComponentBase, IScriptEditor
    {
        [Parameter] public string FilePath { get; set; }
        protected bool ScriptIsChanged { get; set; } = false;
        protected string ErrorText { get; set; } = string.Empty;
        protected string ResultText { get; set; } = string.Empty;
        protected List<DataObject> ResultTable { get; set; } = null;
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override async Task OnParametersSetAsync()
        {
            if (!string.IsNullOrEmpty(FilePath))
            {
                string value = await DaJetClient.GetSourceCode($"/{FilePath}");

                await MonacoEditor.CreateMonacoEditor(this, value);
            }
        }
        public async ValueTask DisposeAsync()
        {
            await MonacoEditor.DisposeMonacoEditor();
        }
        public Task OnScriptChanged(JsonElement element)
        {
            ScriptIsChanged = true;

            StateHasChanged();
            
            return Task.CompletedTask;
        }
        private async Task SaveSourceCode()
        {
            try
            {
                string value = await MonacoEditor.GetMonacoEditorValue();

                string result = await DaJetClient.SaveSourceCode($"/{FilePath}", value);

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
            ResultTable = null;

            try
            {
                string value = await MonacoEditor.GetMonacoEditorValue();

                object result = await DaJetClient.ExecuteScript($"/{FilePath}", value);

                if (result is string content)
                {
                    ResultText = string.IsNullOrEmpty(content) ? "Выполнено успешно" : content;
                }
                else if (result is DataObject row)
                {
                    ResultTable = new List<DataObject>() { row };
                }
                else if (result is List<DataObject> table)
                {
                    ResultTable = table;
                }
            }
            catch (Exception error)
            {
                ErrorText = ExceptionHelper.GetErrorMessage(error);
            }
        }
    }
}