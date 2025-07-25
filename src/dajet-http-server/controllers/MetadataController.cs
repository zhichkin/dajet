using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Services;
using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("md")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public sealed class MetadataController : ControllerBase
    {
        private readonly IDataSource _source;
        private readonly IMetadataService _metadataService;
        public MetadataController(IDataSource source, IMetadataService metadataService)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        }
        
        [HttpGet("")] public ActionResult Select()
        {
            IEnumerable<InfoBaseRecord> list = _source.Query<InfoBaseRecord>();
            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            string json = JsonSerializer.Serialize(list, options);
            return Content(json);
        }
        [HttpGet("{infobase}")] public ActionResult Select([FromRoute] string infobase)
        {
            InfoBaseRecord record = _source.Select<InfoBaseRecord>(infobase);

            if (record == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetInfoBase(in record, out InfoBase entity, out string error))
            {
                return BadRequest(error);
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            string json = JsonSerializer.Serialize(entity, options);

            return Content(json);
        }
        [HttpGet("reset/{infobase}")] public ActionResult ResetCache([FromRoute] string infobase)
        {
            InfoBaseRecord options = _source.Select<InfoBaseRecord>(infobase);

            if (options == null)
            {
                return NotFound();
            }

            if (!Enum.TryParse(options.DatabaseProvider, out DatabaseProvider provider))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Unsupported database provider: {options.DatabaseProvider}");
            }

            string cacheKey;
            try
            {
                cacheKey = DbConnectionFactory.GetCacheKey(provider, options.ConnectionString, options.UseExtensions);
            }
            catch
            {
                return BadRequest("Неверный формат строки подключения!");
            }

            _metadataService.Remove(cacheKey);

            _metadataService.Add(new InfoBaseOptions()
            {
                CacheKey = cacheKey,
                UseExtensions = options.UseExtensions,
                DatabaseProvider = provider,
                ConnectionString = options.ConnectionString
            });

            return Ok();
        }
        [HttpGet("{infobase}/{type}")] public ActionResult SelectMetadataItems([FromRoute] string infobase, [FromRoute] string type)
        {
            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(type))
            {
                return BadRequest();
            }

            InfoBaseRecord entity = _source.Select<InfoBaseRecord>(infobase);

            if (entity == null)
            {
                return NotFound();
            }

            List<MetadataItem> list = new();

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            if (!_metadataService.TryGetMetadataProvider(entity.Identity.ToString(), out IMetadataProvider provider, out string error))
            {
                return NotFound(error);
            }

            Guid uuid = MetadataTypes.ResolveName(type);

            if (uuid == Guid.Empty)
            {
                return NotFound(type);
            }

            foreach (MetadataItem item in provider.GetMetadataItems(uuid))
            {
                if (provider.TryGetExtendedInfo(item.Uuid, out MetadataItemEx extended))
                {
                    if (item.Uuid == extended.Uuid)
                    {
                        continue; // Cобственный объект расширения - в основной конфигурации не показываем
                    }
                }

                list.Add(item);
            }

            string json = JsonSerializer.Serialize(list, options);

            return Content(json);
        }
       
        [HttpPost("")] public ActionResult Insert([FromBody] InfoBaseRecord entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Name) ||
                string.IsNullOrWhiteSpace(entity.ConnectionString) ||
                !Enum.TryParse(entity.DatabaseProvider, out DatabaseProvider provider))
            {
                return BadRequest("Неверно указаны параметры!");
            }

            if (_source.Select<InfoBaseRecord>(entity.Identity) is not null)
            {
                return Conflict();
            }

            string key_to_insert;
            try
            {
                key_to_insert = DbConnectionFactory.GetCacheKey(provider, entity.ConnectionString, entity.UseExtensions);
            }
            catch
            {
                return BadRequest("Неверный формат строки подключения!");
            }

            _source.Create(entity);

            _metadataService.Add(new InfoBaseOptions()
            {
                CacheKey = key_to_insert,
                UseExtensions = entity.UseExtensions,
                DatabaseProvider = provider,
                ConnectionString = entity.ConnectionString
            });

            return Created($"{entity.Name}", $"{entity.Identity}");
        }
        [HttpPut("")] public ActionResult Update([FromBody] InfoBaseRecord entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Name) ||
                string.IsNullOrWhiteSpace(entity.ConnectionString) ||
                !Enum.TryParse(entity.DatabaseProvider, out DatabaseProvider provider))
            {
                return BadRequest("Неверно указаны параметры!");
            }

            InfoBaseRecord record = _source.Select<InfoBaseRecord>(entity.Identity);

            if (record is null)
            {
                return NotFound();
            }

            string key_to_remove;
            try
            {
                key_to_remove = DbConnectionFactory.GetCacheKey(provider, record.ConnectionString, record.UseExtensions);
            }
            catch
            {
                return BadRequest("Неверный формат строки подключения!");
            }

            string key_to_add;
            try
            {
                key_to_add = DbConnectionFactory.GetCacheKey(provider, entity.ConnectionString, entity.UseExtensions);
            }
            catch
            {
                return BadRequest("Неверный формат строки подключения!");
            }

            _source.Update(entity);

            _metadataService.Remove(key_to_remove);

            _metadataService.Add(new InfoBaseOptions()
            {
                CacheKey = key_to_add,
                UseExtensions = entity.UseExtensions,
                DatabaseProvider = provider,
                ConnectionString = entity.ConnectionString
            });

            return Ok();
        }
        [HttpDelete("")] public ActionResult Delete([FromBody] InfoBaseRecord entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                return BadRequest("Неверно указаны параметры!");
            }

            InfoBaseRecord record = _source.Select<InfoBaseRecord>(entity.Identity);

            if (record is null)
            {
                return NotFound();
            }

            if (!Enum.TryParse(record.DatabaseProvider, out DatabaseProvider provider))
            {
                return BadRequest("Неверно указан провайдер данных!");
            }

            string key_to_remove;
            try
            {
                key_to_remove = DbConnectionFactory.GetCacheKey(provider, record.ConnectionString, record.UseExtensions);
            }
            catch
            {
                return BadRequest("Неверный формат строки подключения!");
            }

            _source.Delete(record.GetEntity());

            _metadataService.Remove(key_to_remove);

            return Ok();
        }

        [HttpGet("{infobase}/{type}/{name}")] // ? details = full
        public ActionResult GetMetadataObject([FromRoute] string infobase, [FromRoute] string type, [FromRoute] string name)
        {
            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            InfoBaseRecord entity = _source.Select<InfoBaseRecord>(infobase);

            if (entity == null) { return NotFound(infobase); }

            if (Request.Query["details"].FirstOrDefault() == "full")
            {
                return GetMetadataObjectDetailsFull(in infobase, in type, in name);
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            options.Converters.Add(new DataObjectJsonConverter());

            if (!_metadataService.TryGetMetadataProvider(entity.Identity.ToString(), out IMetadataProvider provider, out string error))
            {
                return NotFound(error);
            }

            Guid uuid = MetadataTypes.ResolveName(type);

            if (uuid == Guid.Empty)
            {
                return NotFound(type);
            }

            MetadataObject metadata;
            MetadataObjectConverter converter = new(provider as OneDbMetadataProvider);

            try
            {
                metadata = provider.GetMetadataObject($"{type}.{name}");
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetErrorMessage(exception));
            }

            if (metadata == null)
            {
                return NotFound();
            }

            DataObject @object = converter.Convert(in metadata);

            string json = JsonSerializer.Serialize(@object, options);

            return Content(json);
        }
        private ActionResult GetMetadataObjectDetailsFull(in string infobase, in string type, in string name)
        {
            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            InfoBaseRecord entity = _source.Select<InfoBaseRecord>(infobase);

            if (entity is null) { return NotFound(infobase); }

            if (!Enum.TryParse(entity.DatabaseProvider, out DatabaseProvider database))
            {
                return NotFound(entity.DatabaseProvider);
            }

            OneDbMetadataProviderOptions options = new()
            {
                UseExtensions = false,
                ResolveReferences = true,
                DatabaseProvider = database,
                ConnectionString = entity.ConnectionString
            };

            if (!OneDbMetadataProvider.TryCreateMetadataProvider(in options, out OneDbMetadataProvider provider, out string error))
            {
                return NotFound(error);
            }

            string metadataName = $"{type}.{name}";

            MetadataObject metadata = provider.GetMetadataObject(metadataName);

            if (metadata is null)
            {
                return NotFound(metadataName);
            }

            DataObject @object;

            try
            {
                @object = new MetadataObjectConverter(in provider).Convert(in metadata);
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetErrorMessage(exception));
            }

            if (@object is null) { return NotFound(metadataName); }

            JsonSerializerOptions JsonOptions = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            JsonOptions.Converters.Add(new DataObjectJsonConverter());

            string json = JsonSerializer.Serialize(@object, JsonOptions);

            return Content(json);
        }

        [HttpGet("diagnostic/{infobase}")] public ActionResult CompareMetadataAndDatabaseSchema([FromRoute] string infobase)
        {
            InfoBaseRecord settings = _source.Select<InfoBaseRecord>(infobase);

            if (settings is null) { return Content($"Настройки базы данных [{infobase}] не найдены!"); }

            string key = settings.Identity.ToString();

            if (!_metadataService.TryGetMetadataProvider(key, out IMetadataProvider provider, out string error))
            {
                return Content($"Провайдер метаданных для [{infobase}] не найден!");
            }

            StringBuilder logger = new();

            CompareMetadataAndDatabaseSchema(in provider, in logger);

            return Content(logger.ToString());
        }
        private static void CompareMetadataAndDatabaseSchema(in IMetadataProvider provider, in StringBuilder logger)
        {
            foreach (Guid type in MetadataTypes.ApplicationObjectTypes)
            {
                foreach (MetadataItem item in provider.GetMetadataItems(type))
                {
                    MetadataObject metadata = provider.GetMetadataObject(item.Type, item.Uuid);

                    if (metadata is ApplicationObject entity)
                    {
                        string entityName = entity.Name;
                        PerformDatabaseSchemaCheck(in provider, in entityName, entity.TableName, entity.Properties, in logger);

                        if (entity is ITablePartOwner owner)
                        {
                            foreach (TablePart table in owner.TableParts)
                            {
                                string tableName = $"{entityName}.{table.Name}";
                                PerformDatabaseSchemaCheck(in provider, in tableName, table.TableName, table.Properties, in logger);
                            }
                        }
                    }
                }
            }
        }
        private static void PerformDatabaseSchemaCheck(in IMetadataProvider provider, in string entityName, in string tableName, in List<MetadataProperty> properties, in StringBuilder logger)
        {
            SqlMetadataReader sql = new();
            sql.UseDatabaseProvider(provider.DatabaseProvider);
            sql.UseConnectionString(provider.ConnectionString);

            MetadataCompareAndMergeService comparator = new();

            List<Metadata.Services.SqlFieldInfo> fields = sql.GetSqlFieldsOrderedByName(tableName);

            List<string> source = comparator.PrepareComparison(fields); // эталон (как должно быть)
            List<string> target = comparator.PrepareComparison(properties); // испытуемый на соответствие эталону

            comparator.Compare(target, source, out List<string> delete_list, out List<string> insert_list);

            if (delete_list.Count == 0 && insert_list.Count == 0)
            {
                return; // success - проверка прошла успешно
            }

            logger.AppendLine($"[{tableName}] {entityName}");

            if (delete_list.Count > 0)
            {
                logger.AppendLine($"* delete (лишние поля)");

                foreach (string field in delete_list)
                {
                    logger.AppendLine($"  - {field}");
                }
            }

            if (insert_list.Count > 0)
            {
                logger.AppendLine($"* insert (отсутствующие поля)");

                foreach (string field in insert_list)
                {
                    logger.AppendLine($"  - {field}");
                }
            }
        }
    }
}