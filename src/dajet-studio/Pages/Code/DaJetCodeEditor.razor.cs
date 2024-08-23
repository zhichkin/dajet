using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using System.Xml.Linq;

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
            //Model.Script = args.Value.ToString();
        }
        protected override void OnInitialized()
        {
            base.OnInitialized();
            Navigator.LocationChanged += Navigator_LocationChanged;
        }
        private void Navigator_LocationChanged(object sender, LocationChangedEventArgs args)
        {
            JSRuntime.InvokeVoidAsync("OpenCodeItemContextMenu", args.Location);
        }
        public void Dispose()
        {
            //NOTE: check for unsaved changes here
            Navigator.LocationChanged -= Navigator_LocationChanged;
        }
        private async Task SaveScript()
        {
            //try
            //{
            //    if (Model.IsNew())
            //    {
            //        await DataSource.CreateAsync(Model);
            //    }
            //    else if (Model.IsChanged())
            //    {
            //        await DataSource.UpdateAsync(Model);
            //    }

            //    ScriptIsChanged = !Model.IsOriginal();
            //}
            //catch (Exception error)
            //{
            //    AppState.FooterText = error.Message;
            //}
        }
        protected async Task ExecuteScript()
        {
            ErrorText = string.Empty;
            ResultText = string.Empty;

            //QueryResponse result = await DaJetClient.ExecuteScriptSql(Model);

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