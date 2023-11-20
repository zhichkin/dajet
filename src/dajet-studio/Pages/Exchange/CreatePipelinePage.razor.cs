using DaJet.Model;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Net.Http.Json;
using System.Web;

namespace DaJet.Studio.Pages.Exchange
{
    public partial class CreatePipelinePage : ComponentBase
    {
        private const string BLOCK_NAME_EXCHANGE = "DaJet.Exchange.{0}.OneDbExchange";
        private const string BLOCK_NAME_ROUTER = "DaJet.Exchange.{0}.OneDbRouter";
        private const string BLOCK_NAME_TRANSFORMER = "DaJet.Exchange.{0}.OneDbTransformer";
        private const string BLOCK_NAME_SERIALIZER = "DaJet.Exchange.OneDbSerializer";
        private const string BLOCK_NAME_PRODUCER = "DaJet.Exchange.{0}.OneDbProducer";
        private const string BLOCK_NAME_RABBITMQ = "DaJet.Exchange.RabbitMQ.Producer";
        private const string BLOCK_NAME_APACHE_KAFKA = "DaJet.Exchange.Kafka.Producer";
        [Parameter] public string Database { get; set; }
        [Parameter] public string Exchange { get; set; }
        protected PipelineModel Model { get; set; } = new();
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
        protected void NavigateToDaJetFlowPage() { Navigator.NavigateTo("/dajet/table"); }
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
            Model.Options.Add(new OptionModel() { Name = "SleepTimeout", Type = "System.Int32", Value = "0" });
            Model.Options.Add(new OptionModel() { Name = "ShowStackTrace", Type = "System.Boolean", Value = "false" });

            Model.Handlers.Clear();
            Model.Handlers.Add(new HandlerModel()
            {
                Name = string.Format(BLOCK_NAME_EXCHANGE, source.DatabaseProvider),
                Options =
                {
                    new OptionModel() { Name = "Source", Type = "System.String", Value = Database },
                    new OptionModel() { Name = "Exchange", Type = "System.String", Value = Exchange },
                    new OptionModel() { Name = "NodeName", Type = "System.String", Value = NodeName },
                    new OptionModel() { Name = "Timeout", Type = "System.Int32", Value = "10" },
                    new OptionModel() { Name = "BatchSize", Type = "System.Int32", Value = "1000" }
                }
            });
            Model.Handlers.Add(new HandlerModel()
            {
                Name = string.Format(BLOCK_NAME_ROUTER, source.DatabaseProvider),
                Options =
                {
                    new OptionModel() { Name = "Source", Type = "System.String", Value = Database },
                    new OptionModel() { Name = "Exchange", Type = "System.String", Value = Exchange },
                    new OptionModel() { Name = "MaxDop", Type = "System.Int32", Value = "1" },
                    new OptionModel() { Name = "Timeout", Type = "System.Int32", Value = "10" }
                }
            });
            Model.Handlers.Add(new HandlerModel()
            {
                Name = string.Format(BLOCK_NAME_TRANSFORMER, source.DatabaseProvider),
                Options =
                {
                    new OptionModel() { Name = "Source", Type = "System.String", Value = Database },
                    new OptionModel() { Name = "Exchange", Type = "System.String", Value = Exchange },
                    new OptionModel() { Name = "MaxDop", Type = "System.Int32", Value = "1" },
                    new OptionModel() { Name = "Timeout", Type = "System.Int32", Value = "10" }
                }
            });
            Model.Handlers.Add(new HandlerModel()
            {
                Name = BLOCK_NAME_SERIALIZER,
                Options =
                {
                    new OptionModel() { Name = "MaxDop", Type = "System.Int32", Value = "1" }
                }
            });

            if (TargetType == "SqlServer" || TargetType == "PostgreSql")
            {
                Model.Handlers.Add(new HandlerModel()
                {
                    Name = string.Format(BLOCK_NAME_PRODUCER, TargetType),
                    Options =
                    {
                        new OptionModel() { Name = "Target", Type = "System.String", Value = TargetUrl.Name },
                        new OptionModel() { Name = "Script", Type = "System.String", Value = InqueueScriptUrl },
                        new OptionModel() { Name = "Timeout", Type = "System.Int32", Value = "10" }
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

                Model.Handlers.Add(new HandlerModel()
                {
                    Name = BLOCK_NAME_RABBITMQ,
                    Options =
                    {
                        new OptionModel() { Name = "Target", Type = "System.String", Value = url },
                        new OptionModel() { Name = "Exchange", Type = "System.String", Value = TopicName },
                        new OptionModel() { Name = "Mandatory", Type = "System.Boolean", Value = "false" },
                        new OptionModel() { Name = "RoutingKey", Type = "System.String", Value = string.Empty },
                        new OptionModel() { Name = "CC", Type = "System.String", Value = string.Empty }
                    }
                });
            }
            else // Apache Kafka
            {
                Model.Handlers.Add(new HandlerModel()
                {
                    Name = BLOCK_NAME_APACHE_KAFKA,
                    Options =
                    {
                        new OptionModel() { Name = "ClientId", Type = "System.String", Value = KafkaClient },
                        new OptionModel() { Name = "BootstrapServers", Type = "System.String", Value = KafkaBroker },
                        new OptionModel() { Name = "Acks", Type = "System.String", Value = "all" },
                        new OptionModel() { Name = "MaxInFlight", Type = "System.String", Value = "1" },
                        new OptionModel() { Name = "MessageTimeoutMs", Type = "System.String", Value = "30000" },
                        new OptionModel() { Name = "EnableIdempotence", Type = "System.String", Value = "false" }
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

            ScriptRecord script = new()
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
            ScriptRecord script = new()
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