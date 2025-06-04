using DaJet.Model;
using DaJet.Model.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text;

namespace DaJet.Studio.Pages.Metadata
{
    public partial class DbViewGeneratorPage : ComponentBase
    {
        [Parameter] public string InfoBase { get; set; }
        public string DatabaseSchema { get; set; } = string.Empty;
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
                    
                    if (DatabaseProvider == "SqlServer")
                    {
                        DatabaseSchema = "dbo";
                    }
                    else // PostgreSql
                    {
                        DatabaseSchema = "public";
                    }
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
        public async Task CreateViews(MouseEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(DatabaseSchema))
            {
                LogText = "Не указана схема базы данных!"; return;
            }

            LogText = "Выполняется...";
            StartTime = DateTime.Now.ToString("HH:mm:ss");
            FinishTime = string.Empty;

            string message = "Создать/обновить представления СУБД ?";

            bool confirmed = await JSRuntime.InvokeAsync<bool>("confirm", message);

            if (!confirmed)
            {
                LogText = "Операция отменена.";
                StartTime = string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(DatabaseProvider))
            {
                LogText = "Не указан провайдер данных!"; return;
            }

            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                LogText = "Не указана строка подключения!"; return;
            }

            StartTime = DateTime.Now.ToString("HH:mm:ss");

            try
            {
                CreateDbViewsResponse response = await DaJetClient.CreateDbViews(InfoBase, DatabaseSchema);

                StringBuilder logger = new();
                
                logger.AppendLine($"Создано {response.Result} представлений.");
                
                foreach (string error in response.Errors)
                {
                    logger.AppendLine(error);
                }

                LogText = logger.ToString();
            }
            catch (Exception error)
            {
                LogText = ExceptionHelper.GetErrorMessage(error);
            }

            FinishTime = DateTime.Now.ToString("HH:mm:ss");
        }
        public async Task DeleteViews(MouseEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(DatabaseSchema))
            {
                LogText = "Не указана схема базы данных!"; return;
            }

            LogText = "Выполняется...";
            StartTime = DateTime.Now.ToString("HH:mm:ss");
            FinishTime = string.Empty;

            string message = "Удалить представления СУБД ?";

            bool confirmed = await JSRuntime.InvokeAsync<bool>("confirm", message);

            if (!confirmed)
            {
                LogText = "Операция отменена.";
                StartTime = string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(DatabaseProvider))
            {
                LogText = "Не указан провайдер данных!"; return;
            }

            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                LogText = "Не указана строка подключения!"; return;
            }

            StartTime = DateTime.Now.ToString("HH:mm:ss");

            try
            {
                LogText = await DaJetClient.DeleteDbViews(InfoBase, DatabaseSchema);
            }
            catch (Exception error)
            {
                LogText = ExceptionHelper.GetErrorMessage(error);
            }

            FinishTime = DateTime.Now.ToString("HH:mm:ss");
        }
    }
}