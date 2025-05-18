using DaJet.Http.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Metadata
{
    public partial class MetadataEntityPage : ComponentBase
    {
        [Parameter] public string Url { get; set; }
        public string Name { get; set; }
        protected override async Task OnParametersSetAsync()
        {
            Url = Url.Replace('_', '/');

            try
            {
                EntityModel entity = await DaJetClient.GetMetadataObject(Url);

                Name = entity.Name;
            }
            catch (Exception error)
            {
                Name = ExceptionHelper.GetErrorMessage(error);
            }
        }
    }
}