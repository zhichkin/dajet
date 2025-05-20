using DaJet.Data;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Metadata
{
    public partial class MetadataObjectPage : ComponentBase
    {
        [Parameter] public string Url { get; set; }
        public dynamic Metadata { get; set; }
        public string Type
        {
            get
            {
                return Metadata is null ? string.Empty : Metadata.Type;
            }
        }
        public int Code
        {
            get
            {
                return Metadata is null ? 0 : Metadata.Code;
            }
        }
        public string Name { get; set; } = string.Empty;
        public List<dynamic> GetProperties()
        {
            List<dynamic> properties = new();

            if (Metadata is null)
            {
                return properties;
            }

            foreach (DataObject property in Metadata.Properties)
            {
                properties.Add(property);
            }

            return properties;
        }
        protected override async Task OnParametersSetAsync()
        {
            Url = Url.Replace('~', '/');

            try
            {
                Metadata = await DaJetClient.GetMetadataObject(Url);
            }
            catch (Exception error)
            {
                Name = ExceptionHelper.GetErrorMessage(error);
            }
        }
    }
}