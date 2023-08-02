using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages.Exchange
{
    public partial class ConfigureExchangeTuning : ComponentBase
    {
        [Parameter] public string Database { get; set; }
        protected bool ShowProgressBar { get; set; } = false;
        protected List<ExchangeTuningLogEntry> ExecutionLog { get; } = new();
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected async Task EnableExchangeTuningCommand()
        {
            ExecutionLog.Clear();

            ShowProgressBar = true;

            try
            {
                HttpResponseMessage response = await Http.PostAsync($"/exchange/configure/tuning/{Database}", null);

                if (response.IsSuccessStatusCode)
                {
                    Snackbar.Add($"Тюнинг [{Database}] планов обмена включен.", Severity.Success);

                    Dictionary<string, string> result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

                    foreach (var item in result)
                    {
                        ExecutionLog.Add(new ExchangeTuningLogEntry()
                        {
                            TypeName = item.Key,
                            Description = item.Value
                        });
                    }
                }
                else
                {
                    string result = await response.Content?.ReadAsStringAsync();

                    string error = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);

                    Snackbar.Add(error, Severity.Error);
                }
            }
            catch (Exception error)
            {
                Snackbar.Add(error.Message, Severity.Error);
            }
            finally
            {
                ShowProgressBar = false;
            }
        }
        protected async Task DisableExchangeTuningCommand()
        {
            ExecutionLog.Clear();

            ShowProgressBar = true;

            try
            {
                HttpResponseMessage response = await Http.DeleteAsync($"/exchange/configure/tuning/{Database}");

                if (response.IsSuccessStatusCode)
                {
                    Snackbar.Add($"Тюнинг [{Database}] планов обмена выключен.", Severity.Warning);

                    Dictionary<string, string> result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

                    foreach (var item in result)
                    {
                        ExecutionLog.Add(new ExchangeTuningLogEntry()
                        {
                            TypeName = item.Key,
                            Description = item.Value
                        });
                    }
                }
                else
                {
                    string result = await response.Content?.ReadAsStringAsync();

                    string error = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);

                    Snackbar.Add(error, Severity.Error);
                }
            }
            catch (Exception error)
            {
                Snackbar.Add(error.Message, Severity.Error);
            }
            finally
            {
                ShowProgressBar = false;
            }
        }
    }
    public sealed class ExchangeTuningLogEntry
    {
        public string TypeName { get; set; }
        public string Description { get; set; }
    }
}