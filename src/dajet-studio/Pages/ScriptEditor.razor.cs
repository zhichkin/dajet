using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages
{
    public partial class ScriptEditor : ComponentBase
    {
        protected string ErrorText { get; set; } = string.Empty;
        protected string ScriptText { get; set; } = "Код скрипта ...";
        protected void CloseScriptEditor()
        {
            Navigator.NavigateTo("/");
        }
        protected void ExecuteScript()
        {
            string script = ScriptText;

            ErrorText = script;
        }
    }
}