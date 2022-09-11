using DaJet.Data;
using DaJet.Http.DataMappers;
using DaJet.Http.Model;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
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
        private readonly InfoBaseDataMapper _mapper = new();
        private readonly IMetadataService _metadataService;
        public MetadataController(IMetadataService metadataService)
        {
            _metadataService = metadataService;
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
            InfoBaseModel? record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetInfoBase(infobase, out InfoBase entity, out string error))
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
            InfoBaseModel? options = _mapper.Select(infobase);

            if (options == null)
            {
                return NotFound();
            }

            if (!Enum.TryParse(options.DatabaseProvider, out DatabaseProvider provider))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Unsupported database privider: {options.DatabaseProvider}");
            }

            _metadataService.Remove(infobase);

            _metadataService.Add(new InfoBaseOptions()
            {
                Key = infobase,
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

            if (!_metadataService.TryGetMetadataCache(infobase, out MetadataCache cache, out string error))
            {
                return NotFound(error);
            }

            Guid uuid = MetadataTypes.ResolveName(type);

            if (uuid == Guid.Empty)
            {
                return NotFound(type);
            }

            List<MetadataItem> list = new();

            foreach (MetadataItem item in cache.GetMetadataItems(uuid))
            {
                list.Add(item);
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            string json = JsonSerializer.Serialize(list, options);

            return Content(json);
        }
        [HttpGet("{infobase}/{type}/{name}")] public ActionResult SelectMetadataObject([FromRoute] string infobase, [FromRoute] string type, [FromRoute] string name)
        {
            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            if (!_metadataService.TryGetMetadataCache(infobase, out MetadataCache cache, out string error))
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
                @object = cache.GetMetadataObject($"{type}.{name}");
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetErrorMessage(exception));
            }

            if (@object == null)
            {
                return NotFound();
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            string json = JsonSerializer.Serialize(@object, @object.GetType(), options);

            return Content(json);
        }

        [HttpPost("")] public ActionResult Insert([FromBody] InfoBaseModel entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Name) ||
                string.IsNullOrWhiteSpace(entity.ConnectionString) ||
                !Enum.TryParse(entity.DatabaseProvider, out DatabaseProvider provider))
            {
                return BadRequest();
            }

            if (_mapper.Select(entity.Name) != null || !_mapper.Insert(entity))
            {
                return Conflict();
            }
            
            _metadataService.Add(new InfoBaseOptions()
            {
                Key = entity.Name,
                DatabaseProvider = provider,
                ConnectionString = entity.ConnectionString
            });

            return Created($"infobase", null);
        }
        [HttpPut("")] public ActionResult Update([FromBody] InfoBaseModel entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Name) ||
                string.IsNullOrWhiteSpace(entity.ConnectionString) ||
                !Enum.TryParse(entity.DatabaseProvider, out DatabaseProvider provider))
            {
                return BadRequest();
            }

            InfoBaseModel record = _mapper.Select(entity.Name)!;

            if (record == null)
            {
                return NotFound();
            }

            if (!_mapper.Update(entity))
            {
                return Conflict();
            }

            _metadataService.Remove(entity.Name);
            _metadataService.Add(new InfoBaseOptions()
            {
                Key = entity.Name,
                DatabaseProvider = provider,
                ConnectionString = entity.ConnectionString
            });

            return Ok();
        }
        [HttpDelete("")] public ActionResult Delete([FromBody] InfoBaseModel entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                return BadRequest();
            }

            InfoBaseModel record = _mapper.Select(entity.Name)!;

            if (record == null)
            {
                return NotFound();
            }

            if (!_mapper.Delete(record))
            {
                return Conflict();
            }

            _metadataService.Remove(entity.Name);

            return Ok();
        }
    }
}