using DaJet.Flow.Model;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages
{
    public partial class DaJetFlowMainPage : ComponentBase
    {
        protected string ErrorText { get; set; }
        protected bool IsLoading { get; set; } = true;
        protected List<PipelineInfo> Pipelines { get; set; } = new();
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected void CreatePipelinePage() { Navigator.NavigateTo("/dajet-flow/pipeline"); }
        protected override async Task OnInitializedAsync()
        {
            await RefreshPipelineList();
        }
        protected async Task RefreshPipelineList()
        {
            IsLoading = true;
            Pipelines.Clear();
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
                    Pipelines = await response.Content.ReadFromJsonAsync<List<PipelineInfo>>();
                }
            }
            catch (Exception error)
            {
                ErrorText = error.Message;
            }

            IsLoading = false;
        }
        protected async Task ExecutePipeline(Guid uuid)
        {
            try
            {
                HttpResponseMessage response = await Http.PutAsync($"/flow/execute/{uuid.ToString().ToLower()}", null);

                if (!response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    ErrorText = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);
                }
            }
            catch (Exception error)
            {
                ErrorText = error.Message;
            }

            await RefreshPipelineList();
        }
        protected async Task DisposePipeline(Guid uuid)
        {
            try
            {
                HttpResponseMessage response = await Http.PutAsync($"/flow/dispose/{uuid.ToString().ToLower()}", null);

                if (!response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    ErrorText = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);
                }
            }
            catch (Exception error)
            {
                ErrorText = error.Message;
            }

            await RefreshPipelineList();
        }
    }
}