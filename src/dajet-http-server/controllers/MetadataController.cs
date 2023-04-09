using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Options;
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
        private readonly InfoBaseDataMapper _mapper;
        private readonly IMetadataService _metadataService;
        public MetadataController(InfoBaseDataMapper mapper, IMetadataService metadataService)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        }
        
        [HttpGet("")] public ActionResult Select()
        {
            List<InfoBaseModel> list = _mapper.Select();
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
            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetInfoBase(record.Uuid.ToString(), out InfoBase entity, out string error))
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
            InfoBaseModel options = _mapper.Select(infobase);

            if (options == null)
            {
                return NotFound();
            }

            if (!Enum.TryParse(options.DatabaseProvider, out DatabaseProvider provider))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Unsupported database privider: {options.DatabaseProvider}");
            }

            string key = options.Uuid.ToString();

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

            InfoBaseModel entity = _mapper.Select(infobase);

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

            if (!_metadataService.TryGetMetadataProvider(entity.Uuid.ToString(), out IMetadataProvider provider, out string error))
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
        [HttpGet("{infobase}/{type}/{name}")] public ActionResult SelectMetadataObject(
            [FromRoute] string infobase, [FromRoute] string type, [FromRoute] string name)
        {
            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            InfoBaseModel entity = _mapper.Select(infobase);

            if (entity == null)
            {
                return NotFound();
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            if (!_metadataService.TryGetMetadataProvider(entity.Uuid.ToString(), out IMetadataProvider provider, out string error))
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

        [HttpPost("")] public ActionResult Insert([FromBody] InfoBaseModel entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Name) ||
                string.IsNullOrWhiteSpace(entity.ConnectionString) ||
                !Enum.TryParse(entity.DatabaseProvider, out DatabaseProvider provider))
            {
                return BadRequest("Неверно указаны параметры!");
            }

            string key = entity.Uuid.ToString();

            if (_mapper.Select(key) != null || !_mapper.Insert(entity))
            {
                return Conflict();
            }
            
            _metadataService.Add(new InfoBaseOptions()
            {
                Key = key,
                UseExtensions = entity.UseExtensions,
                DatabaseProvider = provider,
                ConnectionString = entity.ConnectionString
            });

            return Created($"{entity.Name}", $"{entity.Uuid}");
        }
        [HttpPut("")] public ActionResult Update([FromBody] InfoBaseModel entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Name) ||
                string.IsNullOrWhiteSpace(entity.ConnectionString) ||
                !Enum.TryParse(entity.DatabaseProvider, out DatabaseProvider provider))
            {
                return BadRequest("Неверно указаны параметры!");
            }

            InfoBaseModel record = _mapper.Select(entity.Uuid)!;

            if (record == null)
            {
                return NotFound();
            }

            if (!_mapper.Update(entity))
            {
                return Conflict();
            }

            string key = entity.Uuid.ToString();

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
        [HttpDelete("")] public ActionResult Delete([FromBody] InfoBaseModel entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                return BadRequest("Неверно указаны параметры!");
            }

            InfoBaseModel record = _mapper.Select(entity.Uuid)!;

            if (record == null)
            {
                return NotFound();
            }

            if (!_mapper.Delete(record))
            {
                return Conflict();
            }

            _metadataService.Remove(entity.Uuid.ToString());

            return Ok();
        }
    }
}