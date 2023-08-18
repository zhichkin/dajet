using DaJet.Flow.Model;
using DaJet.Studio.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages.Kafka
{
    public partial class CreateKafkaConsumerPage : ComponentBase
    {
        private const string ONEDB_MESSAGE_NAME = "DaJet.Exchange.OneDbMessage";
        private const string BLOCK_NAME_EXCHANGE = "DaJet.Exchange.{0}.OneDbExchange";
        private const string BLOCK_NAME_ROUTER = "DaJet.Exchange.{0}.OneDbRouter";
        private const string BLOCK_NAME_TRANSFORMER = "DaJet.Exchange.{0}.OneDbTransformer";
        private const string BLOCK_NAME_SERIALIZER = "DaJet.Exchange.OneDbSerializer";
        private const string BLOCK_NAME_PRODUCER = "DaJet.Exchange.{0}.OneDbProducer";
        private const string BLOCK_NAME_RABBITMQ = "DaJet.Exchange.RabbitMQ.Producer";
        private const string BLOCK_NAME_APACHE_KAFKA = "DaJet.Exchange.Kafka.Producer";
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
        protected string InsertScriptUrl { get; set; } = "/insert";
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
                Handler = string.Format(BLOCK_NAME_EXCHANGE, DataSourceUrl.DatabaseProvider),
                Message = ONEDB_MESSAGE_NAME,
                Options =
                {
                    new OptionItem() { Name = "Pipeline", Type = "System.Guid", Value = Model.Uuid.ToString().ToLower() },
                    new OptionItem() { Name = "Source", Type = "System.String", Value = DataSourceUrl.Name },
                    new OptionItem() { Name = "Timeout", Type = "System.Int32", Value = "10" },
                    new OptionItem() { Name = "BatchSize", Type = "System.Int32", Value = "1000" }
                }
            });
            Model.Blocks.Add(new PipelineBlock()
            {
                Handler = string.Format(BLOCK_NAME_ROUTER, DataSourceUrl.DatabaseProvider),
                Message = ONEDB_MESSAGE_NAME,
                Options =
                {
                    new OptionItem() { Name = "Source", Type = "System.String", Value = DataSourceUrl.Name },
                    new OptionItem() { Name = "MaxDop", Type = "System.Int32", Value = "1" },
                    new OptionItem() { Name = "Timeout", Type = "System.Int32", Value = "10" }
                }
            });
            Model.Blocks.Add(new PipelineBlock()
            {
                Handler = string.Format(BLOCK_NAME_TRANSFORMER, DataSourceUrl.DatabaseProvider),
                Message = ONEDB_MESSAGE_NAME,
                Options =
                {
                    new OptionItem() { Name = "Source", Type = "System.String", Value = DataSourceUrl.Name },
                    new OptionItem() { Name = "MaxDop", Type = "System.Int32", Value = "1" },
                    new OptionItem() { Name = "Timeout", Type = "System.Int32", Value = "10" }
                }
            });
            Model.Blocks.Add(new PipelineBlock()
            {
                Handler = BLOCK_NAME_SERIALIZER,
                Message = ONEDB_MESSAGE_NAME,
                Options =
                {
                    new OptionItem() { Name = "MaxDop", Type = "System.Int32", Value = "1" }
                }
            });

            if (DataSourceType == "SqlServer" || DataSourceType == "PostgreSql")
            {
                Model.Blocks.Add(new PipelineBlock()
                {
                    Handler = string.Format(BLOCK_NAME_PRODUCER, DataSourceType),
                    Message = ONEDB_MESSAGE_NAME,
                    Options =
                    {
                        new OptionItem() { Name = "Target", Type = "System.String", Value = DataSourceUrl.Name },
                        new OptionItem() { Name = "Script", Type = "System.String", Value = InsertScriptUrl },
                        new OptionItem() { Name = "Timeout", Type = "System.Int32", Value = "10" }
                    }
                });
            }
            else // Apache Kafka
            {
                Model.Blocks.Add(new PipelineBlock()
                {
                    Handler = BLOCK_NAME_APACHE_KAFKA,
                    Message = ONEDB_MESSAGE_NAME,
                    Options =
                    {
                        new OptionItem() { Name = "Pipeline", Type = "System.Guid", Value = Model.Uuid.ToString().ToLower() },
                        new OptionItem() { Name = "ClientId", Type = "System.String", Value = DataSourceUrl.Name },
                        new OptionItem() { Name = "BootstrapServers", Type = "System.String", Value = KafkaServer },
                        new OptionItem() { Name = "Acks", Type = "System.String", Value = "all" },
                        new OptionItem() { Name = "MaxInFlight", Type = "System.String", Value = "1" },
                        new OptionItem() { Name = "MessageTimeoutMs", Type = "System.String", Value = "30000" },
                        new OptionItem() { Name = "EnableIdempotence", Type = "System.String", Value = "false" }
                    }
                });
            }

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
                HttpResponseMessage response = await Http.PutAsJsonAsync($"/exchange/configure/script/inqueue/{DataSourceUrl.Name}", script);

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