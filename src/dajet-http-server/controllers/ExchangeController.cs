using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Options;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        [HttpPost("{infobase}/{publication}")] public ActionResult CreatePublication([FromRoute] string infobase, [FromRoute] string publication)
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
                throw new InvalidOperationException($"Metadata object not found: {metadataName}");
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

            ScriptRecord route = CreateRouteScriptNode(database.Uuid, pub);
            if (route is null) { return BadRequest(); }

            ScriptRecord document = CreateDocumentScriptNode(database.Uuid, pub);
            if (route is null) { return BadRequest(); }

            ScriptRecord catalog = CreateCatalogScriptNode(database.Uuid, pub);
            if (route is null) { return BadRequest(); }

            ScriptRecord register = CreateRegisterScriptNode(database.Uuid, pub);
            if (route is null) { return BadRequest(); }

            List<ScriptRecord> parents = new() { catalog, document, register };

            CreateArticleScripts(database.Uuid, in parents, in articles);

            return Created($"[{infobase}] {publication}", $"[{infobase}] {publication}");
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
        private ScriptRecord CreateRouteScriptNode(Guid database, ScriptRecord parent)
        {
            ScriptRecord script = new()
            {
                Name = "route",
                IsFolder = false,
                Owner = database,
                Parent = parent.Uuid,
                Script = $"SELECT '{parent.Name}'"
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
        private void CreateArticleScripts(Guid database, in List<ScriptRecord> parents, in List<MetadataObject> articles)
        {
            foreach (MetadataObject article in articles)
            {
                if (article is Catalog catalog) { CreateCatalogScripts(database, parents[0], in catalog); }
                else if (article is Document document) { CreateDocumentScripts(database, parents[1], in document); }
                else if (article is InformationRegister register) { CreateRegisterScripts(database, parents[2], in register); }
                else
                {
                    continue; // unsupported metadata type - no processing ¯\_(ツ)_/¯
                }
            }
        }
        private void CreateCatalogScripts(Guid database, in ScriptRecord parent, in Catalog catalog)
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
                Script = GenerateConsumeScript(in catalog)
            });

            _ = _scripts.Insert(new ScriptRecord()
            {
                IsFolder = false,
                Name = "route",
                Owner = database,
                Parent = script.Uuid,
                Script = GenerateRouteScript(in catalog)
            });

            _ = _scripts.Insert(new ScriptRecord()
            {
                IsFolder = false,
                Name = "contract",
                Owner = database,
                Parent = script.Uuid,
                Script = GenerateContractScript(in catalog)
            });
        }
        private void CreateDocumentScripts(Guid database, in ScriptRecord parent, in Document catalog)
        {

        }
        private void CreateRegisterScripts(Guid database, in ScriptRecord parent, in InformationRegister catalog)
        {

        }
        private string GenerateConsumeScript(in Catalog catalog)
        {
            StringBuilder script = new();

            script.AppendLine("DECLARE @node_name  string;");
            script.AppendLine("DECLARE @batch_size number;");
            script.AppendLine();
            script.AppendLine("CONSUME TOP @batch_size");
            script.AppendLine("           Изменения.Ссылка AS Ссылка");
            script.AppendLine($"     FROM Справочник.{catalog.Name}.Изменения AS Изменения");
            script.AppendLine("INNER JOIN ПланОбмена.ПланОбменаDaJet AS ПланОбмена");
            script.AppendLine("        ON Изменения.УзелОбмена = ПланОбмена.Ссылка");
            script.AppendLine("       AND ПланОбмена.Код = @node_name");

            return script.ToString();
        }
        private string GenerateRouteScript(in Catalog catalog)
        {
            StringBuilder script = new();

            script.AppendLine($"DECLARE @Ссылка Справочник.{catalog.Name};");
            script.AppendLine();
            script.AppendLine($"SELECT 'routing_key' FROM Справочник.{catalog.Name} WHERE Ссылка = @Ссылка");

            return script.ToString();
        }
        private string GenerateContractScript(in Catalog catalog)
        {
            StringBuilder script = new();

            script.AppendLine($"DECLARE @Ссылка Справочник.{catalog.Name};");
            script.AppendLine();
            script.AppendLine("SELECT");
            for (int i = 0; i < catalog.Properties.Count; i++)
            {
                MetadataProperty property = catalog.Properties[i];

                if (i > 0) { script.AppendLine(","); }

                script.Append(property.Name);
            }
            script.AppendLine();
            script.AppendLine($"FROM Справочник.{catalog.Name}");
            script.AppendLine("WHERE Ссылка = @Ссылка");

            foreach (TablePart table in catalog.TableParts)
            {
                script.AppendLine();
                script.AppendLine("SELECT");
                for (int i = 0; i < table.Properties.Count; i++)
                {
                    MetadataProperty property = table.Properties[i];

                    if (i > 0) { script.AppendLine(","); }

                    script.Append(property.Name);
                }
                script.AppendLine();
                script.AppendLine($"FROM Справочник.{catalog.Name}.{table.Name} AS {table.Name}");
                script.AppendLine("WHERE Ссылка = @Ссылка");
            }

            return script.ToString();
        }
    }
}