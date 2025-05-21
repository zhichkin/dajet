using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Extensions;
using DaJet.Metadata.Model;
using DaJet.Model;
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
        private readonly IDataSource _source;
        private readonly IMetadataService _metadataService;
        public ExtensionsController(IDataSource source, IMetadataService metadataService)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        }
        [HttpGet("{infobase}")] public ActionResult Select([FromRoute] string infobase)
        {
            InfoBaseRecord database = _source.Select<InfoBaseRecord>(infobase);

            if (database == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetOneDbMetadataProvider(database.Identity.ToString(), out OneDbMetadataProvider cache, out string error))
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

            InfoBaseRecord database = _source.Select<InfoBaseRecord>(infobase);

            if (database == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetOneDbMetadataProvider(database.Identity.ToString(), out OneDbMetadataProvider cache, out string error))
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

            if (!cache.TryGetMetadata(in info, out OneDbMetadataProvider metadata, out error))
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
        
        [HttpGet("{infobase}/{extension}/{type}/{name}")] // ? details = full
        public ActionResult GetMetadataObject([FromRoute] string infobase, [FromRoute] string extension, [FromRoute] string type, [FromRoute] string name)
        {
            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            InfoBaseRecord database = _source.Select<InfoBaseRecord>(infobase);

            if (database is null)
            {
                return NotFound(infobase);
            }

            string metadataName = $"{type}.{name}";

            if (Request.Query["details"].FirstOrDefault() == "full")
            {
                return GetMetadataObjectDetailsFull(in database, in metadataName);
            }

            if (!_metadataService.TryGetOneDbMetadataProvider(database.Identity.ToString(), out OneDbMetadataProvider cache, out string error))
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

            if (!cache.TryGetMetadata(in info, out OneDbMetadataProvider provider, out error))
            {
                return NotFound($"{extension}: {error}");
            }

            Guid uuid = MetadataTypes.ResolveName(type);

            if (uuid == Guid.Empty)
            {
                return NotFound(type);
            }

            MetadataObject metadata;

            try
            {
                metadata = provider.GetMetadataObject(metadataName);
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetErrorMessage(exception));
            }

            if (metadata == null)
            {
                return NotFound($"{infobase}.{extension}.{type}.{name}");
            }

            MetadataObjectConverter converter = new(provider);
            DataObject @object = converter.Convert(in metadata);

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            options.Converters.Add(new DataObjectJsonConverter());

            string json = JsonSerializer.Serialize(@object, options);

            return Content(json);
        }
        private ActionResult GetMetadataObjectDetailsFull(in InfoBaseRecord database, in string metadataName)
        {
            if (!Enum.TryParse(database.DatabaseProvider, out DatabaseProvider databaseProvider))
            {
                return NotFound(database.DatabaseProvider);
            }

            OneDbMetadataProviderOptions options = new()
            {
                UseExtensions = true,
                ResolveReferences = true,
                DatabaseProvider = databaseProvider,
                ConnectionString = database.ConnectionString
            };

            if (!OneDbMetadataProvider.TryCreateMetadataProvider(in options, out OneDbMetadataProvider provider, out string error))
            {
                return NotFound(error);
            }

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
    }
}