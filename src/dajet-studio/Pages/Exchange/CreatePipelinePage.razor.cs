using DaJet.Flow.Model;
using DaJet.Model;
using DaJet.Studio.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Json;
using System.Web;

namespace DaJet.Studio.Pages.Exchange
{
    public partial class CreatePipelinePage : ComponentBase
    {
        private const string ONEDB_MESSAGE_NAME = "DaJet.Exchange.OneDbMessage";
        private const string BLOCK_NAME_EXCHANGE = "DaJet.Exchange.{0}.OneDbExchange";
        private const string BLOCK_NAME_ROUTER = "DaJet.Exchange.{0}.OneDbRouter";
        private const string BLOCK_NAME_TRANSFORMER = "DaJet.Exchange.{0}.OneDbTransformer";
        private const string BLOCK_NAME_SERIALIZER = "DaJet.Exchange.OneDbSerializer";
        private const string BLOCK_NAME_PRODUCER = "DaJet.Exchange.{0}.OneDbProducer";
        private const string BLOCK_NAME_RABBITMQ = "DaJet.Exchange.RabbitMQ.Producer";
        private const string BLOCK_NAME_APACHE_KAFKA = "DaJet.Exchange.Kafka.Producer";
        [Parameter] public string Database { get; set; }
        [Parameter] public string Exchange { get; set; }
        protected PipelineOptions Model { get; set; } = new();
        protected string NodeName { get; set; } = string.Empty;
        protected List<string> NodeNames { get; set; } = new();
        protected string TargetType { get; set; } = string.Empty;
        protected List<string> TargetTypes { get; set; } = new()
        {
            "Apache Kafka", "PostgreSql", "SqlServer", "RabbitMQ"
        };
        protected InfoBaseRecord TargetUrl { get; set; }
        protected List<InfoBaseRecord> TargetUrls { get; set; } = new();
        protected string MonitorScriptUrl { get; set; } = "/monitor";
        protected bool GenerateMonitorScript { get; set; } = false;
        protected string InqueueScriptUrl { get; set; } = "/inqueue";
        protected bool GenerateInqueueScript { get; set; } = false;
        protected string BrokerUrl { get; set; } = "amqp://guest:guest@localhost:5672";
        protected string VirtualHost { get; set; } = "/";
        protected string TopicName { get; set; } = string.Empty;
        protected string KafkaBroker { get; set; } = "127.0.0.1:9092";
        protected string KafkaClient { get; set; } = string.Empty;
        protected override async Task OnParametersSetAsync()
        {
            Model.Name = Database;
            TargetType = "RabbitMQ";
            VirtualHost = Database;
            TopicName = Exchange;
            KafkaClient = Database;

            await SelectNodeNames();
            await SelectTargetUrls();
        }
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected void NavigateToDaJetFlowPage() { Navigator.NavigateTo("/dajet-flow"); }
        private async Task SelectNodeNames()
        {
            NodeNames.Clear();

            try
            {
                HttpResponseMessage response = await Http.GetAsync($"/exchange/{Database}/{Exchange}/subscribers");

                List<string> list = await response.Content.ReadFromJsonAsync<List<string>>();

                if (response.IsSuccessStatusCode)
                {
                    if (list is not null && list.Count > 0)
                    {
                        NodeName = list[0];
                        NodeNames.AddRange(list);
                    }
                    else
                    {
                        NodeName = string.Empty;
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
        private async Task SelectTargetUrls()
        {
            TargetUrls.Clear();

            try
            {
                HttpResponseMessage response = await Http.GetAsync("/md");

                List<InfoBaseRecord> list = await response.Content.ReadFromJsonAsync<List<InfoBaseRecord>>();

                if (response.IsSuccessStatusCode)
                {
                    if (list is not null && list.Count > 0)
                    {
                        TargetUrl = list[0];

                        foreach (InfoBaseRecord database in list)
                        {
                            TargetUrls.Add(database);
                        }
                    }
                    else
                    {
                        TargetUrl = null;
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
            InfoBaseRecord source = TargetUrls.Where(db => db.Name == Database).FirstOrDefault();

            if (source is null)
            {
                Snackbar.Add($"База данных [{Database}] не найдена!", Severity.Warning); return;
            }

            if (GenerateMonitorScript)
            {
                await GenerateMonitorScriptAtServer();
            }

            await GenerateInqueueScriptAtServer();

            //TODO: move all the following code to the server

            if (string.IsNullOrWhiteSpace(Model.Name))
            {
                Model.Name = Database;
            }

            Model.Activation = ActivationMode.Manual;

            Model.Options.Clear();
            Model.Options.Add(new OptionItem() { Name = "SleepTimeout", Type = "System.Int32", Value = "0" });
            Model.Options.Add(new OptionItem() { Name = "ShowStackTrace", Type = "System.Boolean", Value = "false" });

            Model.Blocks.Clear();
            Model.Blocks.Add(new PipelineBlock()
            {
                Handler = string.Format(BLOCK_NAME_EXCHANGE, source.DatabaseProvider),
                Message = ONEDB_MESSAGE_NAME,
                Options =
                {
                    new OptionItem() { Name = "Pipeline", Type = "System.Guid", Value = Model.Uuid.ToString().ToLower() },
                    new OptionItem() { Name = "Source", Type = "System.String", Value = Database },
                    new OptionItem() { Name = "Exchange", Type = "System.String", Value = Exchange },
                    new OptionItem() { Name = "NodeName", Type = "System.String", Value = NodeName },
                    new OptionItem() { Name = "Timeout", Type = "System.Int32", Value = "10" },
                    new OptionItem() { Name = "BatchSize", Type = "System.Int32", Value = "1000" }
                }
            });
            Model.Blocks.Add(new PipelineBlock()
            {
                Handler = string.Format(BLOCK_NAME_ROUTER, source.DatabaseProvider),
                Message = ONEDB_MESSAGE_NAME,
                Options =
                {
                    new OptionItem() { Name = "Source", Type = "System.String", Value = Database },
                    new OptionItem() { Name = "Exchange", Type = "System.String", Value = Exchange },
                    new OptionItem() { Name = "MaxDop", Type = "System.Int32", Value = "1" },
                    new OptionItem() { Name = "Timeout", Type = "System.Int32", Value = "10" }
                }
            });
            Model.Blocks.Add(new PipelineBlock()
            {
                Handler = string.Format(BLOCK_NAME_TRANSFORMER, source.DatabaseProvider),
                Message = ONEDB_MESSAGE_NAME,
                Options =
                {
                    new OptionItem() { Name = "Source", Type = "System.String", Value = Database },
                    new OptionItem() { Name = "Exchange", Type = "System.String", Value = Exchange },
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

            if (TargetType == "SqlServer" || TargetType == "PostgreSql")
            {
                Model.Blocks.Add(new PipelineBlock()
                {
                    Handler = string.Format(BLOCK_NAME_PRODUCER, TargetType),
                    Message = ONEDB_MESSAGE_NAME,
                    Options =
                    {
                        new OptionItem() { Name = "Target", Type = "System.String", Value = TargetUrl.Name },
                        new OptionItem() { Name = "Script", Type = "System.String", Value = InqueueScriptUrl },
                        new OptionItem() { Name = "Timeout", Type = "System.Int32", Value = "10" }
                    }
                });
            }
            else if (TargetType == "RabbitMQ")
            {
                string url = BrokerUrl;

                if (!string.IsNullOrWhiteSpace(VirtualHost))
                {
                    url += "/" + HttpUtility.UrlEncode(VirtualHost);
                }

                Model.Blocks.Add(new PipelineBlock()
                {
                    Handler = BLOCK_NAME_RABBITMQ,
                    Message = ONEDB_MESSAGE_NAME,
                    Options =
                    {
                        new OptionItem() { Name = "Target", Type = "System.String", Value = url },
                        new OptionItem() { Name = "Exchange", Type = "System.String", Value = TopicName },
                        new OptionItem() { Name = "Mandatory", Type = "System.Boolean", Value = "false" },
                        new OptionItem() { Name = "RoutingKey", Type = "System.String", Value = string.Empty },
                        new OptionItem() { Name = "CC", Type = "System.String", Value = string.Empty }
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
                        new OptionItem() { Name = "ClientId", Type = "System.String", Value = KafkaClient },
                        new OptionItem() { Name = "BootstrapServers", Type = "System.String", Value = KafkaBroker },
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
        private async Task GenerateInqueueScriptAtServer()
        {
            if (!GenerateInqueueScript)
            {
                return;
            }

            if (!(TargetType == "SqlServer" || TargetType == "PostgreSql"))
            {
                return;
            }

            ScriptModel script = new()
            {
                Name = InqueueScriptUrl
            };

            try
            {
                HttpResponseMessage response = await Http.PutAsJsonAsync($"/exchange/configure/script/inqueue/{TargetUrl.Name}", script);

                if (response.IsSuccessStatusCode)
                {
                    Snackbar.Add($"Cкрипт /{TargetUrl.Name}{InqueueScriptUrl} сформирован успешно.", Severity.Success);
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
                Snackbar.Add($"Создание скрипта /{TargetUrl.Name}{InqueueScriptUrl}: {error.Message}", Severity.Warning);
            }
        }
        private async Task GenerateMonitorScriptAtServer()
        {
            ScriptModel script = new()
            {
                Name = MonitorScriptUrl
            };

            try
            {
                string url = $"/exchange/configure/script/monitor/{Database}/{Exchange}/{NodeName}";

                HttpResponseMessage response = await Http.PutAsJsonAsync(url, script);

                if (response.IsSuccessStatusCode)
                {
                    Snackbar.Add($"Cкрипт /{Database}{MonitorScriptUrl} сформирован успешно.", Severity.Success);
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
                Snackbar.Add($"Создание скрипта /{Database}{MonitorScriptUrl}: {error.Message}", Severity.Warning);
            }
        }
    }
}