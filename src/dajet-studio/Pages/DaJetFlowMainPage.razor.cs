using DaJet.Flow.Model;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages
{
    public partial class DaJetFlowMainPage : ComponentBase
    {
        protected string ErrorText { get; set; }
        protected List<PipelineOptions> Pipelines { get; set; }
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected void CreatePipelinePage() { Navigator.NavigateTo("/dajet-flow/pipeline"); }
        protected override async Task OnInitializedAsync()
        {
            await RefreshPipelineList();
        }
        protected async Task RefreshPipelineList()
        {
            Pipelines = null;
            ErrorText = string.Empty;

            try
            {
                Dictionary<string, object> parameters = new();

                HttpResponseMessage response = await Http.GetAsync("/flow");

                if (!response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    ErrorText = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);
                }
                else
                {
                    Pipelines = await response.Content.ReadFromJsonAsync<List<PipelineOptions>>();
                }
            }
            catch (Exception error)
            {
                ErrorText = error.Message;
            }
        }
    }
}