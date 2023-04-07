using DaJet.Flow.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages
{
    public partial class DaJetFlowPipeline : ComponentBase
    {
        [Parameter] public Guid Uuid { get; set; }
        protected PipelineOptions Model { get; set; } = new();
        private bool _is_new_pipeline = true;
        protected override async Task OnParametersSetAsync()
        {
            if (Uuid == Guid.Empty)
            {
                Model.Name = "New pipeline";
                Model.Options = await SelectPipelineOptions();
            }
            else
            {
                _is_new_pipeline = false;
                Model = await SelectPipeline(Uuid); //TODO: handle error if Model is not found by uuid
            }
        }
        private async Task<List<OptionItem>> SelectPipelineOptions()
        {
            List<OptionItem> options = new();

            try
            {
                HttpResponseMessage response = await Http.GetAsync($"/flow/options/DaJet.Flow.Pipeline");

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
                    options = await response.Content.ReadFromJsonAsync<List<OptionItem>>();
                }
            }
            catch (Exception error)
            {
                //ErrorText = error.Message;
            }

            return options;
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
        protected async Task SelectPipelineBlock()
        {
            var settings = new DialogOptions() { CloseButton = true };
            var dialog = DialogService.Show<SelectPipelineBlockDialog>("Select pipeline block", settings);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }
            if (result.Data is not PipelineBlock block) { return; }

            Model.Blocks.Add(block);

            StateHasChanged();
        }
        protected async Task CreateUpdatePipeline()
        {
            try
            {
                HttpResponseMessage response = _is_new_pipeline
                    ? await Http.PostAsJsonAsync("/flow", Model)
                    : await Http.PutAsJsonAsync("/flow", Model);
            }
            catch
            {
                throw;
            }

            Navigator.NavigateTo("/dajet-flow");
        }
        protected async Task DeletePipeline()
        {
            if (_is_new_pipeline) { return; }

            try
            {
                HttpResponseMessage response = await Http.DeleteAsync($"/flow/{Model.Uuid.ToString().ToLower()}");
            }
            catch
            {
                throw;
            }

            Navigator.NavigateTo("/dajet-flow");
        }
    }
}