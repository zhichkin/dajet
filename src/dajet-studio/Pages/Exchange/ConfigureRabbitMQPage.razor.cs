using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages.Exchange
{
    public partial class ConfigureRabbitMQPage : ComponentBase
    {
        [Parameter] public string Database { get; set; }
        [Parameter] public string Exchange { get; set; }
        protected string BrokerUrl { get; set; } = "amqp://guest:guest@localhost:15672";
        protected string VirtualHost { get; set; } = "/";
        protected string TopicName { get; set; } = string.Empty;
        protected string ConfigurationStrategy { get; set; } = "types";
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override void OnParametersSet()
        {
            TopicName = Exchange;
            VirtualHost = Database;
        }
        protected async Task ConfigureCommand()
        {
            try
            {
                Dictionary<string, string> parameters = new()
                {
                    { "Database", Database },
                    { "Exchange", Exchange },
                    { "BrokerUrl", BrokerUrl },
                    { "TopicName", TopicName },
                    { "VirtualHost", VirtualHost },
                    { "Strategy", ConfigurationStrategy }
                };

                HttpResponseMessage response = await Http.PostAsJsonAsync($"/exchange/configure/rabbit", parameters);

                if (response.IsSuccessStatusCode)
                {
                    Snackbar.Add($"Конфигурирование RabbitMQ выполнено успешно.", Severity.Success);
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
        }
    }
}