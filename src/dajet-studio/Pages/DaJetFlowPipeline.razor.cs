using DaJet.Flow.Model;
using DaJet.Studio.Controllers;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages
{
    public partial class DaJetFlowPipeline : ComponentBase
    {
        [Parameter] public Guid Uuid { get; set; }
        protected PipelineOptions Model { get; set; } = new();
        protected Dictionary<string, List<OptionInfo>> OptionsInfo { get; set; } = new();
        protected override async Task OnParametersSetAsync()
        {
            if (Uuid == Guid.Empty)
            {
                Model.Name = "New pipeline";
            }
            else
            {
                Model = await SelectPipeline(Uuid); //TODO: handle error if Model is not found by uuid
            }

            List<OptionInfo> options = await SelectOptions("DaJet.Flow.Pipeline");

            OptionsInfo.Add("DaJet.Flow.Pipeline", options);

            foreach (OptionInfo option in options)
            {
                if (Model.Options.TryGetValue(option.Name, out string value))
                {
                    option.Value = value;
                }
            }

            foreach (PipelineBlock block in Model.Blocks)
            {
                options = await SelectOptions(block.Handler);
                foreach (OptionInfo option in options)
                {
                    if (block.Options.TryGetValue(option.Name, out string value))
                    {
                        option.Value = value;
                    }
                }
                OptionsInfo.Add(block.Handler, options);
            }
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
        private async Task<List<OptionInfo>> SelectOptions(string name)
        {
            List<OptionInfo> options = new();

            try
            {
                HttpResponseMessage response = await Http.GetAsync($"/flow/options/{name}");

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
                    options = await response.Content.ReadFromJsonAsync<List<OptionInfo>>();
                }
            }
            catch (Exception error)
            {
                //ErrorText = error.Message;
            }

            return options;
        }
        protected async Task SelectPipelineBlock()
        {
            var settings = new DialogOptions() { CloseButton = true };
            var dialog = DialogService.Show<SelectPipelineBlockDialog>("Select pipeline block", settings);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }
            if (result.Data is not PipelineBlock block) { return; }

            if (!OptionsInfo.ContainsKey(block.Handler))
            {
                List<OptionInfo> options = await SelectOptions(block.Handler);

                foreach (OptionInfo option in options)
                {
                    if (block.Options.TryGetValue(option.Name, out string value))
                    {
                        option.Value = value;
                    }
                }

                OptionsInfo.Add(block.Handler, options);
            }
            
            Model.Blocks.Add(block);

            StateHasChanged();
        }
        protected async Task CreatePipeline()
        {
            try
            {
                HttpResponseMessage response = await Http.PostAsJsonAsync("/flow", Model);

                Navigator.NavigateTo("/dajet-flow");
            }
            catch
            {
                throw;
            }
        }
    }
}