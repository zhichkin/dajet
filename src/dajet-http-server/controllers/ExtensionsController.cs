using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Extensions;
using DaJet.Metadata.Model;
using DaJet.Options;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("mdex")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public sealed class ExtensionsController : ControllerBase
    {
        private readonly InfoBaseDataMapper _mapper;
        private readonly IMetadataService _metadataService;
        public ExtensionsController(InfoBaseDataMapper mapper, IMetadataService metadataService)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        }
        [HttpGet("{infobase}")] public ActionResult Select([FromRoute] string infobase)
        {
            InfoBaseModel database = _mapper.Select(infobase);

            if (database == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataCache(database.Uuid.ToString(), out MetadataCache cache, out string error))
            {
                return NotFound(error);
            }

            List<ExtensionInfo> extensions;
            try
            {
                extensions = cache.GetExtensions();
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetErrorMessage(exception));
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            string json = JsonSerializer.Serialize(extensions, options);

            return Content(json);
        }
        [HttpGet("{infobase}/{extension}/{type}")] public ActionResult SelectMetadataItems(
            [FromRoute] string infobase, [FromRoute] string extension, [FromRoute] string type)
        {
            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(type))
            {
                return BadRequest();
            }

            InfoBaseModel database = _mapper.Select(infobase);

            if (database == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataCache(database.Uuid.ToString(), out MetadataCache cache, out string error))
            {
                return NotFound(error);
            }

            ExtensionInfo info;
            try
            {
                info = cache.GetExtension(extension);
            }
            catch (Exception exception)
            {
                return NotFound($"{extension}: {ExceptionHelper.GetErrorMessage(exception)}");
            }

            if (!cache.TryGetMetadata(in info, out MetadataCache metadata, out error))
            {
                return NotFound($"{extension}: {error}");
            }

            Guid uuid = MetadataTypes.ResolveName(type);

            if (uuid == Guid.Empty)
            {
                return NotFound(type);
            }

            List<MetadataItem> list = new();

            foreach (MetadataItem item in metadata.GetMetadataItems(uuid))
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
        [HttpGet("{infobase}/{extension}/{type}/{name}")] public ActionResult SelectMetadataObject(
            [FromRoute] string infobase, [FromRoute] string extension, [FromRoute] string type, [FromRoute] string name)
        {
            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            InfoBaseModel database = _mapper.Select(infobase);

            if (database == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataCache(database.Uuid.ToString(), out MetadataCache cache, out string error))
            {
                return NotFound(error);
            }

            ExtensionInfo info;
            try
            {
                info = cache.GetExtension(extension);
            }
            catch (Exception exception)
            {
                return NotFound($"{extension}: {ExceptionHelper.GetErrorMessage(exception)}");
            }

            if (!cache.TryGetMetadata(in info, out MetadataCache metadata, out error))
            {
                return NotFound($"{extension}: {error}");
            }

            Guid uuid = MetadataTypes.ResolveName(type);

            if (uuid == Guid.Empty)
            {
                return NotFound(type);
            }

            MetadataObject entity;

            try
            {
                entity = metadata.GetMetadataObject($"{type}.{name}");
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetErrorMessage(exception));
            }

            if (entity == null)
            {
                return NotFound($"{infobase}.{extension}.{type}.{name}");
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            string json = JsonSerializer.Serialize(entity, entity.GetType(), options);

            return Content(json);
        }
    }
}