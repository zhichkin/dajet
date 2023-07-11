using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Options;
using DaJet.RabbitMQ.HttpApi;
using DaJet.Scripting;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("exchange")] public class ExchangeController : ControllerBase
    {
        private readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private readonly ScriptDataMapper _scripts;
        private readonly InfoBaseDataMapper _databases;
        private readonly IMetadataService _metadata;
        public ExchangeController(InfoBaseDataMapper databases, ScriptDataMapper scripts, IMetadataService metadata)
        {
            _databases = databases ?? throw new ArgumentNullException(nameof(databases));
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }
        [HttpGet("{infobase}")] public ActionResult Select([FromRoute] string infobase)
        {
            InfoBaseModel database = _databases.Select(infobase);

            if (database is null)
            {
                return NotFound();
            }

            ScriptRecord exchange = _scripts.SelectScriptByPath(database.Uuid, "/exchange");

            if (exchange is null)
            {
                exchange = new ScriptRecord()
                {
                    Owner = database.Uuid,
                    Name = "exchange"
                };
                _scripts.Insert(exchange);
            }

            List<ScriptRecord> list = _scripts.Select(database.Uuid, exchange.Uuid);

            foreach (ScriptRecord parent in list)
            {
                _scripts.GetScriptChildren(parent);
            }

            string json = JsonSerializer.Serialize(list, JsonOptions);

            return Content(json);
        }
        [HttpGet("{infobase}/{publication}")] public ActionResult Select([FromRoute] string infobase, [FromRoute] string publication)
        {
            InfoBaseModel database = _databases.Select(infobase);

            if (database is null)
            {
                return NotFound();
            }

            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, $"/exchange/{publication}");

            if (script is null)
            {
                return NotFound();
            }

            _scripts.GetScriptChildren(script);

            string json = JsonSerializer.Serialize(script, JsonOptions);

            return Content(json);
        }
        [HttpGet("{infobase}/publications")] public ActionResult SelectPublications([FromRoute] string infobase)
        {
            InfoBaseModel database = _databases.Select(infobase);

            if (database is null)
            {
                return NotFound();
            }

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            List<string> list = new();

            foreach (MetadataItem item in provider.GetMetadataItems(MetadataTypes.Publication))
            {
                list.Add(item.Name);
            }

            string json = JsonSerializer.Serialize(list, JsonOptions);

            return Content(json);
        }
        [HttpGet("{infobase}/{publication}/subscribers")] public ActionResult SelectSubscribers([FromRoute] string infobase, [FromRoute] string publication)
        {
            InfoBaseModel database = _databases.Select(infobase);

            if (database is null)
            {
                return NotFound();
            }

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            List<string> subscribers;
            try
            {
                subscribers = GetSubscribers(in publication, in provider);
            }
            catch (Exception exception)
            {
                return BadRequest(ExceptionHelper.GetErrorMessage(exception));
            }

            string json = JsonSerializer.Serialize(subscribers, JsonOptions);

            return Content(json);
        }
        [HttpDelete("{infobase}/{publication}")] public ActionResult DeletePublication([FromRoute] string infobase, [FromRoute] string publication)
        {
            InfoBaseModel database = _databases.Select(infobase);

            if (database is null)
            {
                return NotFound();
            }

            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, $"/exchange/{publication}");

            if (script is null)
            {
                return NotFound();
            }

            if (!_scripts.TrySelect(script.Uuid, out _))
            {
                return NotFound();
            }

            _scripts.DeleteScriptFolder(in script);

            return Ok();
        }
        
        [HttpPost("{infobase}/{publication}/{type}/{article}")]
        public ActionResult CreateArticle([FromRoute] string infobase, [FromRoute] string publication, [FromRoute] string type, [FromRoute] string article)
        {
            InfoBaseModel database = _databases.Select(infobase);

            if (database is null) { return NotFound(); }

            ScriptRecord parent = _scripts.SelectScriptByPath(database.Uuid, $"/exchange/{publication}/pub/{type}");

            if (parent is null) { return NotFound(); }

            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, $"/exchange/{publication}/pub/{type}/{article}");

            if (script is not null) { return BadRequest($"Article {article} exists!"); }

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            string metadataName = "ПланОбмена." + publication;

            if (provider.GetMetadataObject(metadataName) is not Publication exchange)
            {
                return BadRequest($"Metadata object not found: {metadataName}");
            }

            List<MetadataObject> articles = GetPublicationArticles(in exchange, in provider);

            MetadataObject entity = null;
            foreach (MetadataObject metadata in articles)
            {
                if (metadata.Name == article)
                {
                    entity = metadata; break;
                }
            }
            if (entity is null) { return NotFound(); }

            if (entity is Catalog catalog && type == "Справочник")
            {
                CreateCatalogScripts(database.Uuid, in publication, parent, in catalog);
            }
            else if (entity is Document document && type == "Документ")
            {
                CreateDocumentScripts(database.Uuid, in publication, parent, in document);
            }
            else if (entity is InformationRegister register && type == "РегистрСведений")
            {
                CreateRegisterScripts(database.Uuid, in publication, parent, in register, in provider);
            }
            else
            {
                return BadRequest($"Unsupported metadata type \"{entity}\"");
            }

            script = _scripts.SelectScriptByPath(database.Uuid, $"/exchange/{publication}/pub/{type}/{article}");

            if (script is null)
            {
                return BadRequest();
            }

            _scripts.GetScriptChildren(script);

            string json = JsonSerializer.Serialize(script, JsonOptions);

            Response.StatusCode = (int)HttpStatusCode.Created;

            return Content(json, "application/json", Encoding.UTF8);
        }
        
        [HttpDelete("{infobase}/{publication}/{type}/{article}")]
        public ActionResult DeleteArticle([FromRoute] string infobase, [FromRoute] string publication, [FromRoute] string type, [FromRoute] string article)
        {
            InfoBaseModel database = _databases.Select(infobase);

            if (database is null) { return NotFound(); }

            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, $"/exchange/{publication}/pub/{type}/{article}");

            if (script is not null)
            {
                _scripts.DeleteScriptFolder(in script);
            }

            return Ok();
        }

        private List<string> GetSubscribers(in string publication, in IMetadataProvider provider)
        {
            List<string> subscribers = new();

            StringBuilder script = new();
            script.AppendLine($"DECLARE @EmptyUuid uuid;");
            script.AppendLine($"SELECT Код FROM ПланОбмена.{publication} WHERE Предопределённый = @EmptyUuid ORDER BY Код ASC");

            ScriptExecutor executor = new(provider, _metadata, _databases, _scripts);
            executor.Parameters.Add("ThisNode", Guid.Empty);

            try
            {
                foreach (var record in executor.ExecuteReader(script.ToString()))
                {
                    foreach (var column in record)
                    {
                        if (column.Value is string value)
                        {
                            subscribers.Add(value);
                        }
                    }
                }
            }
            catch
            {
                throw;
            }

            return subscribers;
        }
        [HttpPost("configure/rabbit")] public ActionResult ConfigureRabbitMQ([FromBody] Dictionary<string, string> options)
        {
            if (!options.TryGetValue("Database", out string infobase) || string.IsNullOrWhiteSpace(infobase))
            {
                return BadRequest($"Parameter \"Database\" missing.");
            }

            if (!options.TryGetValue("Exchange", out string publication) || string.IsNullOrWhiteSpace(publication))
            {
                return BadRequest($"Parameter \"Exchange\" missing.");
            }

            if (!options.TryGetValue("BrokerUrl", out string brokerurl) || string.IsNullOrWhiteSpace(brokerurl))
            {
                return BadRequest($"Parameter \"BrokerUrl\" missing.");
            }

            InfoBaseModel database = _databases.Select(infobase);

            if (database is null) { return NotFound($"Database [{infobase}] not found."); }

            ScriptRecord exchange = _scripts.SelectScriptByPath(database.Uuid, "/exchange");

            if (exchange is null) { return NotFound("Exchange service not found."); }

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            string metadataName = "ПланОбмена." + publication;

            if (provider.GetMetadataObject(metadataName) is not Publication entity)
            {
                return BadRequest($"Metadata object not found: {metadataName}");
            }

            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, $"/exchange/{publication}");

            if (script is null)
            {
                return NotFound($"Publication {publication} not found.");
            }

            if (!options.TryGetValue("Strategy", out string strategy) || string.IsNullOrWhiteSpace(strategy))
            {
                strategy = "types"; // default configuration strategy
            }

            if (strategy == "types")
            {
                List<MetadataObject> articles = GetPublicationArticles(in entity, in provider);

                if (!TryConfigureRabbitMQ(in options, in articles, out error))
                {
                    return BadRequest(error);
                }
            }
            else if (strategy == "nodes")
            {
                List<string> subscribers = GetSubscribers(in publication, in provider);

                if (!TryConfigureRabbitMQ(in options, in subscribers, out error))
                {
                    return BadRequest(error);
                }
            }
            else
            {
                return BadRequest($"Unknown RabbitMQ configuration strategy: [{strategy}]");
            }

            return Created(new Uri($"rabbit/{database.Name}/{publication}", UriKind.Relative), null);
        }
        private void ConfigureExchange(in IRabbitMQHttpManager manager, out ExchangeInfo exchange)
        {
            VirtualHostInfo vhost = manager.GetVirtualHost(manager.VirtualHost).Result;

            if (vhost is null)
            {
                manager.CreateVirtualHost(manager.VirtualHost).Wait();
                _ = manager.GetVirtualHost(manager.VirtualHost).Result;
            }

            exchange = manager.GetExchange(manager.Exchange).Result;

            if (exchange is null)
            {
                manager.CreateExchange(manager.Exchange).Wait();
                exchange = manager.GetExchange(manager.Exchange).Result;
            }
        }
        private bool TryConfigureRabbitMQ(in Dictionary<string, string> options, in List<MetadataObject> articles, out string error)
        {
            error = string.Empty;

            using (IRabbitMQHttpManager manager = new RabbitMQHttpManager())
            {
                manager.Configure(in options);

                ConfigureExchange(in manager, out ExchangeInfo exchange);

                ConfigureByArticles(in manager, in exchange, in articles);
            }

            return string.IsNullOrEmpty(error);
        }
        private bool TryConfigureRabbitMQ(in Dictionary<string, string> options, in List<string> subscribers, out string error)
        {
            error = string.Empty;

            using (IRabbitMQHttpManager manager = new RabbitMQHttpManager())
            {
                manager.Configure(in options);

                ConfigureExchange(in manager, out ExchangeInfo exchange);

                ConfigureBySubscribers(in manager, in exchange, in subscribers);
            }

            return string.IsNullOrEmpty(error);
        }
        private void ConfigureByArticles(in IRabbitMQHttpManager manager, in ExchangeInfo exchange, in List<MetadataObject> articles)
        {
            foreach (MetadataObject article in articles)
            {
                string queueName = article.Name;

                if (article is Catalog catalog)
                {
                    queueName = $"Справочник.{catalog.Name}";
                }
                else if (article is Document document)
                {
                    queueName = $"Документ.{document.Name}";
                }
                else if (article is InformationRegister register)
                {
                    queueName = $"РегистрСведений.{register.Name}";
                }

                QueueInfo queue = manager.GetQueue(queueName).Result;

                if (queue == null)
                {
                    manager.CreateQueue(queueName).Wait();
                    queue = manager.GetQueue(queueName).Result;
                }

                List<BindingInfo> bindings = manager.GetBindings(queue).Result;

                bool exists = false;

                foreach (BindingInfo binding in bindings)
                {
                    if (binding.Source == exchange.Name)
                    {
                        exists = true; break;
                    }
                }

                if (!exists)
                {
                    manager.CreateBinding(exchange, queue, queue.Name).Wait();
                }
            }
        }
        private void ConfigureBySubscribers(in IRabbitMQHttpManager manager, in ExchangeInfo exchange, in List<string> subscribers)
        {
            foreach (string subscriber in subscribers)
            {
                string queueName = subscriber;

                QueueInfo queue = manager.GetQueue(queueName).Result;

                if (queue == null)
                {
                    manager.CreateQueue(queueName).Wait();
                    queue = manager.GetQueue(queueName).Result;
                }

                List<BindingInfo> bindings = manager.GetBindings(queue).Result;

                bool exists = false;

                foreach (BindingInfo binding in bindings)
                {
                    if (binding.Source == exchange.Name)
                    {
                        exists = true; break;
                    }
                }

                if (!exists)
                {
                    manager.CreateBinding(exchange, queue, queue.Name).Wait();
                }
            }
        }
        [HttpDelete("configure/rabbit")] public ActionResult RabbitMQDeleteVirtualHost([FromBody] Dictionary<string, string> options)
        {
            if (!options.TryGetValue("BrokerUrl", out string brokerurl) || string.IsNullOrWhiteSpace(brokerurl))
            {
                return BadRequest($"Parameter \"BrokerUrl\" missing.");
            }

            using (IRabbitMQHttpManager manager = new RabbitMQHttpManager())
            {
                manager.Configure(in options);

                manager.DeleteVirtualHost(manager.VirtualHost).Wait();
            }

            return Ok();
        }
        

        [HttpPost("{infobase}/{publication}")]
        public ActionResult CreatePublication([FromRoute] string infobase, [FromRoute] string publication)
        {
            InfoBaseModel database = _databases.Select(infobase);

            if (database is null)
            {
                return NotFound();
            }

            ScriptRecord exchange = _scripts.SelectScriptByPath(database.Uuid, "/exchange");

            if (exchange is null)
            {
                exchange = new ScriptRecord()
                {
                    Owner = database.Uuid,
                    Name = "exchange"
                };
                _scripts.Insert(exchange);
            }

            if (!_metadata.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            string metadataName = "ПланОбмена." + publication;

            if (provider.GetMetadataObject(metadataName) is not Publication entity)
            {
                return BadRequest($"Metadata object not found: {metadataName}");
            }

            ScriptRecord script = _scripts.SelectScriptByPath(database.Uuid, $"/exchange/{publication}");
            
            if (script is not null)
            {
                return BadRequest($"Publication {publication} exists!");
            }

            List<MetadataObject> articles = GetPublicationArticles(in entity, in provider);

            script = new ScriptRecord()
            {
                Name = publication,
                Owner = database.Uuid,
                Parent = exchange.Uuid
            };

            if (!_scripts.Insert(script))
            {
                return BadRequest();
            }

            ScriptRecord pub = CreatePubScriptNode(database.Uuid, script);
            if (pub is null) { return BadRequest(); }

            ScriptRecord route = CreateRouteScriptNode(database.Uuid, pub, in publication);
            if (route is null) { return BadRequest(); }

            ScriptRecord document = CreateDocumentScriptNode(database.Uuid, pub);
            if (route is null) { return BadRequest(); }

            ScriptRecord catalog = CreateCatalogScriptNode(database.Uuid, pub);
            if (route is null) { return BadRequest(); }

            ScriptRecord register = CreateRegisterScriptNode(database.Uuid, pub);
            if (route is null) { return BadRequest(); }

            List<ScriptRecord> parents = new() { catalog, document, register };

            CreateArticleScripts(database.Uuid, in publication, in parents, in articles, in provider);

            return Created(new Uri($"/{database.Name}/{publication}", UriKind.Relative), null);
        }
        private List<MetadataObject> GetPublicationArticles(in Publication publication, in IMetadataProvider provider)
        {
            List<MetadataObject> articles = new();

            List<Guid> types = new() { MetadataTypes.Catalog, MetadataTypes.Document, MetadataTypes.InformationRegister };

            foreach (Guid uuid in publication.Articles.Keys)
            {
                foreach (Guid type in types)
                {
                    MetadataObject article = provider.GetMetadataObject(type, uuid);

                    if (article is not null)
                    {
                        articles.Add(article); break;
                    }
                }
            }

            return articles;
        }
        private ScriptRecord CreatePubScriptNode(Guid database, ScriptRecord parent)
        {
            ScriptRecord script = new()
            {
                Name = "pub",
                Owner = database,
                Parent = parent.Uuid
            };

            if (_scripts.Insert(script))
            {
                return script;
            }
            else
            {
                return null;
            }
        }
        private ScriptRecord CreateRouteScriptNode(Guid database, ScriptRecord parent, in string publication)
        {
            ScriptRecord script = new()
            {
                Name = "route", // default script for all of the articles
                IsFolder = false,
                Owner = database,
                Parent = parent.Uuid,
                Script = $"SELECT '{publication}'"
            };

            if (_scripts.Insert(script))
            {
                return script;
            }
            else
            {
                return null;
            }
        }
        private ScriptRecord CreateDocumentScriptNode(Guid database, ScriptRecord parent)
        {
            ScriptRecord script = new()
            {
                Name = "Документ",
                Owner = database,
                Parent = parent.Uuid
            };

            if (_scripts.Insert(script))
            {
                return script;
            }
            else
            {
                return null;
            }
        }
        private ScriptRecord CreateCatalogScriptNode(Guid database, ScriptRecord parent)
        {
            ScriptRecord script = new()
            {
                Name = "Справочник",
                Owner = database,
                Parent = parent.Uuid
            };

            if (_scripts.Insert(script))
            {
                return script;
            }
            else
            {
                return null;
            }
        }
        private ScriptRecord CreateRegisterScriptNode(Guid database, ScriptRecord parent)
        {
            ScriptRecord script = new()
            {
                Name = "РегистрСведений",
                Owner = database,
                Parent = parent.Uuid
            };

            if (_scripts.Insert(script))
            {
                return script;
            }
            else
            {
                return null;
            }
        }
        
        private void CreateArticleScripts(Guid database, in string publication, in List<ScriptRecord> parents, in List<MetadataObject> articles, in IMetadataProvider provider)
        {
            foreach (MetadataObject article in articles)
            {
                if (article is Catalog catalog) { CreateCatalogScripts(database, in publication, parents[0], in catalog); }
                else if (article is Document document) { CreateDocumentScripts(database, in publication, parents[1], in document); }
                else if (article is InformationRegister register) { CreateRegisterScripts(database, in publication, parents[2], in register, in provider); }
                else
                {
                    continue; // unsupported metadata type - no processing ¯\_(ツ)_/¯
                }
            }
        }
        
        private void CreateCatalogScripts(Guid database, in string publication, in ScriptRecord parent, in Catalog catalog)
        {
            ScriptRecord script = new()
            {
                Name = catalog.Name,
                Owner = database,
                Parent = parent.Uuid
            };
            _ = _scripts.Insert(script);

            _ = _scripts.Insert(new ScriptRecord()
            {
                IsFolder = false,
                Name = "consume",
                Owner = database,
                Parent = script.Uuid,
                Script = GenerateConsumeScript(in catalog, in publication)
            });

            _ = _scripts.Insert(new ScriptRecord()
            {
                IsFolder = false,
                Name = "_route", // script is disabled by default
                Owner = database,
                Parent = script.Uuid,
                Script = GenerateRouteScript(in catalog, in publication)
            });

            _ = _scripts.Insert(new ScriptRecord()
            {
                IsFolder = false,
                Name = "contract",
                Owner = database,
                Parent = script.Uuid,
                Script = GenerateContractScript(in catalog, in publication)
            });
        }
        private string GenerateConsumeScript(in Catalog catalog, in string publication)
        {
            StringBuilder script = new();

            script.AppendLine("DECLARE @node_name  string;");
            script.AppendLine("DECLARE @batch_size number;");
            script.AppendLine();
            script.AppendLine("CONSUME TOP @batch_size");
            script.AppendLine("            Изменения.Ссылка AS Ссылка");
            script.AppendLine($"       FROM Справочник.{catalog.Name}.Изменения AS Изменения");
            script.AppendLine($" INNER JOIN ПланОбмена.{publication} AS ПланОбмена");
            script.AppendLine("         ON Изменения.УзелОбмена = ПланОбмена.Ссылка");
            script.AppendLine("        AND ПланОбмена.Код = @node_name");

            return script.ToString();
        }
        private string GenerateRouteScript(in Catalog catalog, in string publication)
        {
            StringBuilder script = new();

            script.AppendLine($"DECLARE @Ссылка Справочник.{catalog.Name};");
            script.AppendLine();
            script.AppendLine($"CREATE COMPUTED TABLE routing_key AS (SELECT @Ссылка AS Ссылка)");
            script.AppendLine($"   SELECT CASE WHEN source.Ссылка IS NULL THEN 'deleted' ELSE 'Справочник.{catalog.Name}' END");
            script.AppendLine( "     FROM routing_key AS rk");
            script.AppendLine($"LEFT JOIN Справочник.{catalog.Name} AS source ON source.Ссылка = rk.Ссылка");

            return script.ToString();
        }
        private string GenerateContractScript(in Catalog catalog, in string publication)
        {
            int line = 0;

            StringBuilder script = new();
            script.AppendLine($"DECLARE @Ссылка Справочник.{catalog.Name};");
            script.AppendLine();
            script.AppendLine("SELECT");
            for (int i = 0; i < catalog.Properties.Count; i++)
            {
                MetadataProperty property = catalog.Properties[i];

                if (property.Purpose == PropertyPurpose.System && property.Name != "Предопределённый")
                {
                    if (line > 0) { script.AppendLine(","); }

                    script.Append(property.Name); line++;
                }
            }
            script.AppendLine();
            script.AppendLine($"FROM Справочник.{catalog.Name}");
            script.AppendLine("WHERE Ссылка = @Ссылка");
            
            foreach (TablePart table in catalog.TableParts)
            {
                line = 0;
                script.AppendLine();
                script.AppendLine("SELECT");
                for (int i = 0; i < table.Properties.Count; i++)
                {
                    MetadataProperty property = table.Properties[i];

                    if (property.Name == "KeyField")
                    {
                        continue;
                    }

                    if (line > 0) { script.AppendLine(","); }

                    script.Append(property.Name); line++;
                }
                script.AppendLine();
                script.AppendLine($"FROM Справочник.{catalog.Name}.{table.Name} AS {table.Name}");
                script.AppendLine("WHERE Ссылка = @Ссылка");
            }

            return script.ToString();
        }

        private void CreateDocumentScripts(Guid database, in string publication, in ScriptRecord parent, in Document document)
        {
            ScriptRecord script = new()
            {
                Name = document.Name,
                Owner = database,
                Parent = parent.Uuid
            };
            _ = _scripts.Insert(script);

            _ = _scripts.Insert(new ScriptRecord()
            {
                IsFolder = false,
                Name = "consume",
                Owner = database,
                Parent = script.Uuid,
                Script = GenerateConsumeScript(in document, in publication)
            });

            _ = _scripts.Insert(new ScriptRecord()
            {
                IsFolder = false,
                Name = "_route", // script is disabled by default
                Owner = database,
                Parent = script.Uuid,
                Script = GenerateRouteScript(in document, in publication)
            });

            _ = _scripts.Insert(new ScriptRecord()
            {
                IsFolder = false,
                Name = "contract",
                Owner = database,
                Parent = script.Uuid,
                Script = GenerateContractScript(in document, in publication)
            });
        }
        private string GenerateConsumeScript(in Document document, in string publication)
        {
            StringBuilder script = new();

            script.AppendLine("DECLARE @node_name  string;");
            script.AppendLine("DECLARE @batch_size number;");
            script.AppendLine();
            script.AppendLine("CONSUME TOP @batch_size");
            script.AppendLine("            Изменения.Ссылка AS Ссылка");
            script.AppendLine($"       FROM Документ.{document.Name}.Изменения AS Изменения");
            script.AppendLine($" INNER JOIN ПланОбмена.{publication} AS ПланОбмена");
            script.AppendLine("         ON Изменения.УзелОбмена = ПланОбмена.Ссылка");
            script.AppendLine("        AND ПланОбмена.Код = @node_name");

            return script.ToString();
        }
        private string GenerateRouteScript(in Document document, in string publication)
        {
            StringBuilder script = new();

            script.AppendLine($"DECLARE @Ссылка Документ.{document.Name};");
            script.AppendLine();
            script.AppendLine($"CREATE COMPUTED TABLE routing_key AS (SELECT @Ссылка AS Ссылка)");
            script.AppendLine($"   SELECT CASE WHEN source.Ссылка IS NULL THEN 'deleted' ELSE 'Документ.{document.Name}' END");
            script.AppendLine("     FROM routing_key AS rk");
            script.AppendLine($"LEFT JOIN Документ.{document.Name} AS source ON source.Ссылка = rk.Ссылка");

            return script.ToString();
        }
        private string GenerateContractScript(in Document document, in string publication)
        {
            int line = 0;

            StringBuilder script = new();
            script.AppendLine($"DECLARE @Ссылка Документ.{document.Name};");
            script.AppendLine();
            script.AppendLine("SELECT");
            for (int i = 0; i < document.Properties.Count; i++)
            {
                MetadataProperty property = document.Properties[i];

                if (property.Purpose == PropertyPurpose.System)
                {
                    if (line > 0) { script.AppendLine(","); }

                    script.Append(property.Name); line++;
                }
            }
            script.AppendLine();
            script.AppendLine($"FROM Документ.{document.Name}");
            script.AppendLine("WHERE Ссылка = @Ссылка");

            foreach (TablePart table in document.TableParts)
            {
                line = 0;
                script.AppendLine();
                script.AppendLine("SELECT");
                for (int i = 0; i < table.Properties.Count; i++)
                {
                    MetadataProperty property = table.Properties[i];

                    if (property.Name == "KeyField")
                    {
                        continue;
                    }

                    if (line > 0) { script.AppendLine(","); }

                    script.Append(property.Name); line++;
                }
                script.AppendLine();
                script.AppendLine($"FROM Документ.{document.Name}.{table.Name} AS {table.Name}");
                script.AppendLine("WHERE Ссылка = @Ссылка");
            }

            return script.ToString();
        }

        private void CreateRegisterScripts(Guid database, in string publication, in ScriptRecord parent, in InformationRegister register, in IMetadataProvider provider)
        {
            MetadataObject entity = provider.GetMetadataObject($"РегистрСведений.{register.Name}.Изменения");

            if (entity is not ChangeTrackingTable table)
            {
                return; // объект метаданных не включён ни в один план обмена
            }

            ScriptRecord script = new()
            {
                Name = register.Name,
                Owner = database,
                Parent = parent.Uuid
            };
            _ = _scripts.Insert(script);

            _ = _scripts.Insert(new ScriptRecord()
            {
                IsFolder = false,
                Name = "consume",
                Owner = database,
                Parent = script.Uuid,
                Script = GenerateConsumeScript(in table, in publication)
            });

            _ = _scripts.Insert(new ScriptRecord()
            {
                IsFolder = false,
                Name = "_route", // script is disabled by default
                Owner = database,
                Parent = script.Uuid,
                Script = GenerateRouteScript(in table, in publication)
            });

            _ = _scripts.Insert(new ScriptRecord()
            {
                IsFolder = false,
                Name = "contract",
                Owner = database,
                Parent = script.Uuid,
                Script = GenerateContractScript(in table, in publication)
            });
        }
        private string GenerateConsumeScript(in ChangeTrackingTable table, in string publication)
        {
            if (table.Entity is not InformationRegister register) { return string.Empty; }

            StringBuilder script = new();

            script.AppendLine("DECLARE @node_name  string;");
            script.AppendLine("DECLARE @batch_size number;");
            script.AppendLine();
            script.AppendLine("CONSUME TOP @batch_size");
            int line = 0;
            for (int i = 0; i < table.Properties.Count; i++)
            {
                MetadataProperty property = table.Properties[i];

                if (property.Name == "УзелОбмена" || property.Name == "НомерСообщения")
                {
                    continue;
                }

                if (line > 0) { script.AppendLine(","); }

                script.Append($"Изменения.{property.Name} AS {property.Name}"); line++;
            }
            script.AppendLine();
            script.AppendLine($"      FROM РегистрСведений.{register.Name}.Изменения AS Изменения");
            script.AppendLine($"INNER JOIN ПланОбмена.{publication} AS ПланОбмена");
            script.AppendLine("        ON Изменения.УзелОбмена = ПланОбмена.Ссылка");
            script.AppendLine("       AND ПланОбмена.Код = @node_name");

            return script.ToString();
        }
        private string GenerateRouteScript(in ChangeTrackingTable table, in string publication)
        {
            if (table.Entity is not InformationRegister register) { return string.Empty; }

            StringBuilder script = new();

            for (int i = 0; i < table.Properties.Count; i++)
            {
                MetadataProperty property = table.Properties[i];

                if (property.Name == "УзелОбмена" || property.Name == "НомерСообщения")
                {
                    continue;
                }

                string type = GetDataTypeLiteral(in property);

                script.AppendLine($"DECLARE @{property.Name} {type};");
            }
            script.AppendLine();
            script.AppendLine($"CREATE COMPUTED TABLE routing_key AS");
            script.AppendLine("(");
            script.AppendLine("SELECT");
            int line = 0;
            for (int i = 0; i < table.Properties.Count; i++)
            {
                MetadataProperty property = table.Properties[i];

                if (property.Name == "УзелОбмена" || property.Name == "НомерСообщения")
                {
                    continue;
                }

                if (line > 0) { script.AppendLine(","); }

                script.Append($"@{property.Name} AS {property.Name}"); line++;
            }
            script.AppendLine();
            script.AppendLine(")");

            string column = register.Properties[0].Name;

            script.AppendLine($"   SELECT CASE WHEN source.{column} IS NULL THEN 'deleted' ELSE 'РегистрСведений.{register.Name}' END");
            script.AppendLine("     FROM routing_key AS rk");
            script.AppendLine($"LEFT JOIN РегистрСведений.{register.Name} AS source");
            line = 0;
            for (int i = 0; i < table.Properties.Count; i++)
            {
                MetadataProperty property = table.Properties[i];

                if (property.Name == "УзелОбмена" || property.Name == "НомерСообщения")
                {
                    continue;
                }

                if (line == 0) { script.Append("ON "); }
                else { script.Append("AND "); }

                script.AppendLine($"source.{property.Name} = rk.{property.Name}"); line++;
            }

            return script.ToString();
        }
        private string GenerateContractScript(in ChangeTrackingTable table, in string publication)
        {
            if (table.Entity is not InformationRegister register) { return string.Empty; }
            
            StringBuilder script = new();
            for (int i = 0; i < table.Properties.Count; i++)
            {
                MetadataProperty property = table.Properties[i];

                if (property.Name == "УзелОбмена" || property.Name == "НомерСообщения")
                {
                    continue;
                }

                string type = GetDataTypeLiteral(in property);

                script.AppendLine($"DECLARE @{property.Name} {type};");
            }
            script.AppendLine();
            script.AppendLine("SELECT");
            int line = 0;
            for (int i = 0; i < register.Properties.Count; i++)
            {
                MetadataProperty property = register.Properties[i];

                if (line > 0) { script.AppendLine(","); }

                script.Append($"{property.Name}"); line++;
            }
            script.AppendLine();
            script.AppendLine($"FROM РегистрСведений.{register.Name}");
            line = 0;
            for (int i = 0; i < table.Properties.Count; i++)
            {
                MetadataProperty property = table.Properties[i];

                if (property.Name == "УзелОбмена" || property.Name == "НомерСообщения")
                {
                    continue;
                }

                if (line == 0) { script.Append("WHERE "); }
                else { script.Append("AND "); }

                script.AppendLine($"{property.Name} = @{property.Name}"); line++;
            }

            return script.ToString();
        }

        private string GetDataTypeLiteral(in MetadataProperty property)
        {
            string type = property.PropertyType.GetTypeLiteral();

            if (type == "entity" || type == "union")
            {
                type = "uuid";
            }

            return type;
        }
    }
}