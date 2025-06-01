using DaJet.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DaJet.Studio.Pages.Metadata
{
    public partial class MetadataDiagnosticPage : ComponentBase
    {
        [Parameter] public string InfoBase { get; set; }
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseProvider { get; set; } = string.Empty;
        public string LogText { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string FinishTime { get; set; } = string.Empty;
        protected override async Task OnParametersSetAsync()
        {
            LogText = string.Empty;
            StartTime = string.Empty;
            FinishTime = string.Empty;

            try
            {
                InfoBaseRecord infoBase = await DaJetClient.SelectAsync<InfoBaseRecord>(InfoBase);

                if (infoBase is not null)
                {
                    DatabaseProvider = infoBase.DatabaseProvider;
                    ConnectionString = infoBase.ConnectionString;
                }
            }
            catch (Exception error)
            {
                LogText = ExceptionHelper.GetErrorMessage(error);
            }
        }
        public void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        public async Task DoDiagnostic(MouseEventArgs args)
        {
            LogText = "Выполняется...";
            StartTime = DateTime.Now.ToString("HH:mm:ss");
            FinishTime = string.Empty;

            if (string.IsNullOrWhiteSpace(DatabaseProvider))
            {
                LogText = "Не указан провайдер данных!"; return;
            }

            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                LogText = "Не указана строка подключения!"; return;
            }

            try
            {
                LogText = await DaJetClient.CompareMetadataAndDatabaseSchema(InfoBase);
            }
            catch (Exception error)
            {
                LogText = ExceptionHelper.GetErrorMessage(error);
            }

            FinishTime = DateTime.Now.ToString("HH:mm:ss");
        }
    }
}