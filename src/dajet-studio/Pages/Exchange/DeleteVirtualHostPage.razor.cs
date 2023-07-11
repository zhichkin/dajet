using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Studio.Pages.Exchange
{
    public partial class DeleteVirtualHostPage : ComponentBase
    {
        private readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        [Parameter] public string Database { get; set; }
        protected string BrokerUrl { get; set; } = "http://guest:guest@localhost:15672";
        protected string VirtualHost { get; set; } = "/";
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override void OnParametersSet()
        {
            VirtualHost = Database;
        }
        protected async Task ConfigureCommand()
        {
            try
            {
                Dictionary<string, string> options = new()
                {
                    { "Database", Database },
                    { "BrokerUrl", BrokerUrl },
                    { "VirtualHost", VirtualHost }
                };

                string json = JsonSerializer.Serialize(options, JsonOptions);

                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Delete,
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                    RequestUri = new Uri($"{Http.BaseAddress}exchange/configure/rabbit", UriKind.Absolute)
                };

                HttpResponseMessage response = await Http.SendAsync(request);

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