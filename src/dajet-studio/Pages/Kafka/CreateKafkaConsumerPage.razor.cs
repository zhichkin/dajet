using DaJet.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages.Kafka
{
    public partial class CreateKafkaConsumerPage : ComponentBase
    {
        private const string BLOCK_NAME_KAFKA_CONSUMER = "DaJet.Flow.Kafka.Consumer";
        private const string BLOCK_NAME_TRANSFORMER = "DaJet.Flow.Kafka.MessageToRecordTransformer";
        private const string BLOCK_NAME_DATABASE_PRODUCER = "DaJet.Flow.{0}.OneDbProducer";
        protected PipelineModel Model { get; set; } = new();
        protected string KafkaServer { get; set; } = "127.0.0.1:9092";
        protected string KafkaTopic { get; set; } = string.Empty;
        protected string PackageName { get; set; } = string.Empty;
        protected string MessageType { get; set; } = string.Empty;
        protected string DataSourceType { get; set; } = string.Empty;
        protected List<string> DataSourceTypes { get; set; } = new()
        {
            "SqlServer", "PostgreSql"
        };
        protected InfoBaseRecord DataSourceUrl { get; set; }
        protected List<InfoBaseRecord> DataSourceUrls { get; set; } = new();
        protected string InsertScriptUrl { get; set; } = "/produce";
        protected bool GenerateInsertScript { get; set; } = false;
        protected override async Task OnParametersSetAsync()
        {
            DataSourceType = "SqlServer";
            await SelectDataSourceUrls();
        }
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected void NavigateToDaJetFlowPage() { Navigator.NavigateTo("/dajet/table"); }
        private async Task SelectDataSourceUrls()
        {
            DataSourceUrls.Clear();

            try
            {
                HttpResponseMessage response = await Http.GetAsync("/md");

                List<InfoBaseRecord> list = await response.Content.ReadFromJsonAsync<List<InfoBaseRecord>>();

                if (response.IsSuccessStatusCode)
                {
                    if (list is not null && list.Count > 0)
                    {
                        DataSourceUrl = list[0];

                        foreach (InfoBaseRecord database in list)
                        {
                            DataSourceUrls.Add(database);
                        }
                    }
                    else
                    {
                        DataSourceUrl = null;
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
        }
        protected async Task CreatePipeline()
        {
            //TODO: move all the following code to the server

            await GenerateInsertScriptAtServer();

            Model.Activation = ActivationMode.Manual;

            Model.Options.Clear();
            Model.Options.Add(new OptionModel() { Name = "SleepTimeout", Type = "System.Int32", Value = "0" });
            Model.Options.Add(new OptionModel() { Name = "ShowStackTrace", Type = "System.Boolean", Value = "false" });

            Model.Handlers.Clear();
            Model.Handlers.Add(new HandlerModel()
            {
                Name = BLOCK_NAME_KAFKA_CONSUMER,
                Options =
                    {
                        new OptionModel() { Name = "GroupId", Type = "System.String", Value = DataSourceUrl.Name },
                        new OptionModel() { Name = "ClientId", Type = "System.String", Value = DataSourceUrl.Name },
                        new OptionModel() { Name = "BootstrapServers", Type = "System.String", Value = KafkaServer },
                        new OptionModel() { Name = "Topic", Type = "System.String", Value = KafkaTopic },
                        new OptionModel() { Name = "AutoOffsetReset", Type = "System.String", Value = "earliest" },
                        new OptionModel() { Name = "EnableAutoCommit", Type = "System.String", Value = "false" },
                        new OptionModel() { Name = "SessionTimeoutMs", Type = "System.String", Value = "60000" },
                        new OptionModel() { Name = "HeartbeatIntervalMs", Type = "System.String", Value = "20000" }
                    }
            });
            Model.Handlers.Add(new HandlerModel()
            {
                Name = BLOCK_NAME_TRANSFORMER,
                Options =
                {
                    new OptionModel() { Name = "PackageName", Type = "System.String", Value = PackageName },
                    new OptionModel() { Name = "MessageType", Type = "System.String", Value = MessageType },
                    new OptionModel()
                    {
                        Name = "ContentType",
                        Type = "System.String",
                        Value = string.IsNullOrWhiteSpace(PackageName) ? string.Empty : "protobuf"
                    }
                }
            });
            Model.Handlers.Add(new HandlerModel()
            {
                Name = string.Format(BLOCK_NAME_DATABASE_PRODUCER, DataSourceType),
                Options =
                {
                    new OptionModel() { Name = "Target", Type = "System.String", Value = DataSourceUrl.Name },
                    new OptionModel() { Name = "Script", Type = "System.String", Value = InsertScriptUrl },
                    new OptionModel() { Name = "Timeout", Type = "System.Int32", Value = "10" }
                }
            });

            try
            {
                HttpResponseMessage response = await Http.PostAsJsonAsync("/flow", Model);

                if (response.IsSuccessStatusCode)
                {
                    NavigateToDaJetFlowPage();
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
        private async Task GenerateInsertScriptAtServer()
        {
            if (!GenerateInsertScript) { return; }

            string scriptUrl = DataSourceUrl.Name + "/" + InsertScriptUrl.TrimStart('/');

            ScriptRecord script = await DataSource.SelectAsync<ScriptRecord>(scriptUrl);

            try
            {
                HttpResponseMessage response = await Http.PutAsJsonAsync($"/exchange/configure/script/produce/{DataSourceUrl.Name}", script);

                if (response.IsSuccessStatusCode)
                {
                    Snackbar.Add($"Cкрипт /{DataSourceUrl.Name}{InsertScriptUrl} сформирован успешно.", Severity.Success);
                }
                else
                {
                    string result = await response.Content?.ReadAsStringAsync();

                    string error = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);

                    Snackbar.Add(error, Severity.Warning);
                }
            }
            catch (Exception error)
            {
                Snackbar.Add($"Создание скрипта /{DataSourceUrl.Name}{InsertScriptUrl}: {error.Message}", Severity.Warning);
            }
        }
    }
}