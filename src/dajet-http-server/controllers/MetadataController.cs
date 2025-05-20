using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
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

            if (!_metadataService.TryGetInfoBase(record.Identity.ToString(), out InfoBase entity, out string error))
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
                return StatusCode(StatusCodes.Status500InternalServerError, $"Unsupported database privider: {options.DatabaseProvider}");
            }

            string key = options.Identity.ToString();

            _metadataService.Remove(key);

            _metadataService.Add(new InfoBaseOptions()
            {
                Key = key,
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

            string key = entity.Identity.ToString();

            if (_source.Select<InfoBaseRecord>(entity.Identity) is not null)
            {
                return Conflict();
            }

            _source.Create(entity);

            _metadataService.Add(new InfoBaseOptions()
            {
                Key = key,
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

            _source.Update(entity);

            string key = entity.Identity.ToString();

            _metadataService.Remove(key);

            _metadataService.Add(new InfoBaseOptions()
            {
                Key = key,
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

            _source.Delete(record.GetEntity());

            _metadataService.Remove(entity.Identity.ToString());

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

            if (!_metadataService.TryGetMetadataProvider(entity.Identity.ToString(), out IMetadataProvider provider, out string error))
            {
                return NotFound(error);
            }

            Guid uuid = MetadataTypes.ResolveName(type);

            if (uuid == Guid.Empty)
            {
                return NotFound(type);
            }

            MetadataObject @object;

            try
            {
                @object = provider.GetMetadataObject($"{type}.{name}");
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetErrorMessage(exception));
            }

            if (@object == null)
            {
                return NotFound();
            }

            string json = JsonSerializer.Serialize(@object, @object.GetType(), options);

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

            DataObject description;

            try
            {
                description = new MetadataObjectConverter(in provider).Convert(in metadata);
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetErrorMessage(exception));
            }

            if (description is null) { return NotFound(metadataName); }

            JsonSerializerOptions JsonOptions = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            JsonOptions.Converters.Add(new DataObjectJsonConverter());

            string json = JsonSerializer.Serialize(description, JsonOptions);

            return Content(json);
        }
    }
}