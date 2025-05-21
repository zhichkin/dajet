using DaJet.Http.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Metadata
{
    public partial class MetadataObjectPage : ComponentBase
    {
        [Parameter] public string Url { get; set; }
        public EntityModel Model { get; set; } = new();
        protected override async Task OnParametersSetAsync()
        {
            Url = Url.Replace('~', '/');

            try
            {
                Model = await DaJetClient.GetMetadataObject(Url);
            }
            catch (Exception error)
            {
                //ErrorMessage = ExceptionHelper.GetErrorMessage(error);
            }
        }
    }
}