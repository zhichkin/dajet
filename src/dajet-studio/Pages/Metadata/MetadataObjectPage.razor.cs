using DaJet.Http.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DaJet.Studio.Pages.Metadata
{
    public partial class MetadataObjectPage : ComponentBase
    {
        [Parameter] public string Url { get; set; }
        public EntityModel Model { get; set; } = new();
        public string PopupTitle { get; set; } = string.Empty;
        public EntityModel SelectedEntity { get; set; } = new();
        public PropertyModel SelectedProperty { get; set; } = new();
        private Dictionary<PropertyModel, bool> PropertyColumnPopupStates { get; } = new();
        public string PropertyReferencesPopupId { get; } = "metadata-object-property-referenses-popup";
        protected override async Task OnParametersSetAsync()
        {
            Url = Url.Replace('~', '/');

            try
            {
                Model = await DaJetClient.GetMetadataObject(Url);

                if (Model is not null)
                {
                    ActivateTab(Model);
                }
            }
            catch (Exception error)
            {
                Url = ExceptionHelper.GetErrorMessage(error);
            }
        }
        public void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        public string GetMetadataObjectIconPath()
        {
            return $"/img/1c/16/{SelectedEntity.Type}.png";
        }
        public string GetPropertyIconPath(PropertyModel property)
        {
            return $"/img/1c/16/{property.Purpose}.png";
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
        public async void ShowPropertyReferencesPopup(PropertyModel property)
        {
            PopupTitle = property.Name;
            SelectedProperty = property;
            await JSRuntime.InvokeVoidAsync("OpenDaJetModalDialog", PropertyReferencesPopupId);
        }
        public async void HidePropertyReferencesPopup()
        {
            PopupTitle = string.Empty;
            SelectedProperty = new PropertyModel();
            await JSRuntime.InvokeVoidAsync("CloseDaJetModalDialog", PropertyReferencesPopupId);
        }
        public void ActivateTab(EntityModel model)
        {
            SelectedEntity = model;
        }
        public string GetTabStyle(EntityModel model)
        {
            if (SelectedEntity == model)
            {
                return "metadata-object-tab-title-selected";
            }
            else
            {
                return "metadata-object-tab-title-normal";
            }
        }
        public string GetTabTitle(EntityModel model)
        {
            return (Model == model) ? "Свойства объекта" : model.Name;
        }
    }
}