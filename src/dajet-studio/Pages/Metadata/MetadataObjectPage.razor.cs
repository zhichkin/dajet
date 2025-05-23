using DaJet.Http.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Metadata
{
    public partial class MetadataObjectPage : ComponentBase
    {
        [Parameter] public string Url { get; set; }
        public EntityModel Model { get; set; } = new();
        private Dictionary<PropertyModel, bool> PropertyColumnPopupStates { get; } = new();
        protected override async Task OnParametersSetAsync()
        {
            Url = Url.Replace('~', '/');

            try
            {
                Model = await DaJetClient.GetMetadataObject(Url);
            }
            catch (Exception error)
            {
                //TODO: ErrorMessage = ExceptionHelper.GetErrorMessage(error);
            }
        }
        public bool IsPropertyColumnPopupActive(PropertyModel property)
        {
            if (PropertyColumnPopupStates.TryGetValue(property, out bool state))
            {
                return state;
            }
            return false;
        }
        public void ShowPropertyColumnPopup(PropertyModel property)
        {
            _ = PropertyColumnPopupStates.TryAdd(property, true);
        }
        public void HidePropertyColumnPopup(PropertyModel property)
        {
            _ = PropertyColumnPopupStates.Remove(property);
        }
        public void ShowPropertyReferencesPopup(PropertyModel property)
        {
            
        }
    }
}