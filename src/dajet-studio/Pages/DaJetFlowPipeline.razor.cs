using DaJet.Flow.Model;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages
{
    public partial class DaJetFlowPipeline : ComponentBase
    {
        [Parameter] public Guid Uuid { get; set; }
        protected PipelineOptions Model { get; set; } = new();
        protected override async Task OnParametersSetAsync()
        {
            if (Uuid == Guid.Empty)
            {
                Model.Name = "New pipeline";
            }
            else
            {
                Model = await SelectPipeline(Uuid);
            }
            //TODO: handle error if Model is not found by uuid
        }
        private async Task<PipelineOptions> SelectPipeline(Guid uuid)
        {
            PipelineOptions pipeline = null;

            try
            {
                HttpResponseMessage response = await Http.GetAsync($"/flow/{uuid}");

                if (!response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    //ErrorText = response.ReasonPhrase
                    //    + (string.IsNullOrEmpty(result)
                    //    ? string.Empty
                    //    : Environment.NewLine + result);
                }
                else
                {
                    pipeline = await response.Content.ReadFromJsonAsync<PipelineOptions>();
                }
            }
            catch (Exception error)
            {
                //ErrorText = error.Message;
            }

            return pipeline;
        }
    }
}