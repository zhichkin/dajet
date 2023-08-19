using DaJet.Flow.Model;
using DaJet.Studio.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages.Kafka
{
    public partial class CreateKafkaConsumerPage : ComponentBase
    {
        private const string BLOCK_NAME_KAFKA_CONSUMER = "DaJet.Flow.Kafka.Consumer";
        private const string MESSAGE_NAME_CONSUME_RESULT = "Confluent.Kafka.ConsumeResult`2[System.Byte[],System.Byte[]]";
        private const string BLOCK_NAME_TRANSFORMER = "DaJet.Flow.Kafka.MessageToRecordTransformer";
        private const string MESSAGE_NAME_DATA_RECORD = "System.Data.IDataRecord";
        private const string BLOCK_NAME_DATABASE_PRODUCER = "DaJet.Flow.{0}.OneDbProducer";
        protected PipelineOptions Model { get; set; } = new();
        protected string KafkaServer { get; set; } = "127.0.0.1:9092";
        protected string KafkaTopic { get; set; } = string.Empty;
        protected string PackageName { get; set; } = string.Empty;
        protected string MessageType { get; set; } = string.Empty;
        protected string DataSourceType { get; set; } = string.Empty;
        protected List<string> DataSourceTypes { get; set; } = new()
        {
            "SqlServer", "PostgreSql"
        };
        protected InfoBaseModel DataSourceUrl { get; set; }
        protected List<InfoBaseModel> DataSourceUrls { get; set; } = new();
        protected string InsertScriptUrl { get; set; } = "/produce";
        protected bool GenerateInsertScript { get; set; } = false;
        protected override async Task OnParametersSetAsync()
        {
            DataSourceType = "SqlServer";
            await SelectDataSourceUrls();
        }
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected void NavigateToDaJetFlowPage() { Navigator.NavigateTo("/dajet-flow"); }
        private async Task SelectDataSourceUrls()
        {
            DataSourceUrls.Clear();

            try
            {
                HttpResponseMessage response = await Http.GetAsync("/md");

                List<InfoBaseModel> list = await response.Content.ReadFromJsonAsync<List<InfoBaseModel>>();

                if (response.IsSuccessStatusCode)
                {
                    if (list is not null && list.Count > 0)
                    {
                        DataSourceUrl = list[0];

                        foreach (InfoBaseModel database in list)
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
            Model.Options.Add(new OptionItem() { Name = "SleepTimeout", Type = "System.Int32", Value = "0" });
            Model.Options.Add(new OptionItem() { Name = "ShowStackTrace", Type = "System.Boolean", Value = "false" });

            Model.Blocks.Clear();
            Model.Blocks.Add(new PipelineBlock()
            {
                Handler = BLOCK_NAME_KAFKA_CONSUMER,
                Message = MESSAGE_NAME_CONSUME_RESULT,
                Options =
                    {
                        new OptionItem() { Name = "Pipeline", Type = "System.Guid", Value = Model.Uuid.ToString().ToLower() },
                        new OptionItem() { Name = "GroupId", Type = "System.String", Value = DataSourceUrl.Name },
                        new OptionItem() { Name = "ClientId", Type = "System.String", Value = DataSourceUrl.Name },
                        new OptionItem() { Name = "BootstrapServers", Type = "System.String", Value = KafkaServer },
                        new OptionItem() { Name = "Topic", Type = "System.String", Value = KafkaTopic },
                        new OptionItem() { Name = "AutoOffsetReset", Type = "System.String", Value = "earliest" },
                        new OptionItem() { Name = "EnableAutoCommit", Type = "System.String", Value = "false" },
                        new OptionItem() { Name = "SessionTimeoutMs", Type = "System.String", Value = "60000" },
                        new OptionItem() { Name = "HeartbeatIntervalMs", Type = "System.String", Value = "20000" }
                    }
            });
            Model.Blocks.Add(new PipelineBlock()
            {
                Handler = BLOCK_NAME_TRANSFORMER,
                Message = MESSAGE_NAME_DATA_RECORD,
                Options =
                {
                    new OptionItem() { Name = "PackageName", Type = "System.String", Value = PackageName },
                    new OptionItem() { Name = "MessageType", Type = "System.String", Value = MessageType },
                    new OptionItem()
                    {
                        Name = "ContentType",
                        Type = "System.String",
                        Value = string.IsNullOrWhiteSpace(PackageName) ? string.Empty : "protobuf"
                    }
                }
            });
            Model.Blocks.Add(new PipelineBlock()
            {
                Handler = string.Format(BLOCK_NAME_DATABASE_PRODUCER, DataSourceType),
                Message = MESSAGE_NAME_DATA_RECORD,
                Options =
                {
                    new OptionItem() { Name = "Target", Type = "System.String", Value = DataSourceUrl.Name },
                    new OptionItem() { Name = "Script", Type = "System.String", Value = InsertScriptUrl },
                    new OptionItem() { Name = "Timeout", Type = "System.Int32", Value = "10" }
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

            ScriptModel script = new()
            {
                Name = InsertScriptUrl
            };

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