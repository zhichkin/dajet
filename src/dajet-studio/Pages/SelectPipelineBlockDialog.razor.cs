using DaJet.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages
{
    public partial class SelectPipelineBlockDialog : ComponentBase
    {
        [CascadingParameter] MudDialogInstance MudDialog { get; set; }
        protected List<PipelineBlock> PipelineBlocks { get; set; } = new();
        protected override async Task OnInitializedAsync()
        {
            PipelineBlocks = await GetPipelineBlocks();
        }
        protected void CloseDialog() { MudDialog.Cancel(); }
        private async Task<List<PipelineBlock>> GetPipelineBlocks()
        {
            List<PipelineBlock> list = new();

            try
            {
                HttpResponseMessage response = await Http.GetAsync($"/flow/blocks");

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
                    list = await response.Content.ReadFromJsonAsync<List<PipelineBlock>>();
                }
            }
            catch (Exception error)
            {
                //ErrorText = error.Message;
            }

            return list;
        }
        protected void OnSelectPipelineBlock(TableRowClickEventArgs<PipelineBlock> args)
        {
            if (args.Item is PipelineBlock block)
            {
                MudDialog.Close(block);
            }
        }
    }
}