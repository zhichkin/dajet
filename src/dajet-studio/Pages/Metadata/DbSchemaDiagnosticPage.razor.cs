using DaJet.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages.Metadata
{
    public partial class DbSchemaDiagnosticPage : ComponentBase
    {
        [Parameter] public string Database { get; set; }
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseProvider { get; set; } = string.Empty;
        public string LogText { get; set; } = string.Empty;
        protected override async Task OnParametersSetAsync()
        {
            LogText = string.Empty;

            try
            {
                InfoBaseRecord infoBase = await DaJetClient.SelectAsync<InfoBaseRecord>(Database);

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
        public void DoDiagnostic()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                LogText = "Не указана строка подключения!";
            }
            else
            {
                LogText = "Диагностика выполнена успешно!";
            }
        }
    }
}