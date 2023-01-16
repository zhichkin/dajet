using DaJet.Http.DataMappers;
using DaJet.Http.Model;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("db/view")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public class DbViewController : ControllerBase
    {
        private const string INFOBASE_IS_NOT_FOUND_ERROR = "InfoBase [{0}] is not found. Try register it with the /md service first.";

        private readonly IFileProvider _fileProvider;
        private readonly IMetadataService _metadataService;
        private readonly InfoBaseDataMapper _mapper = new();
        public DbViewController(IMetadataService metadataService, IFileProvider fileProvider)
        {
            _fileProvider = fileProvider;
            _metadataService = metadataService;
        }

        [HttpGet("select/{infobase}")] public ActionResult Select([FromRoute] string infobase, [FromQuery] string schema = null)
        {
            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound(string.Format(INFOBASE_IS_NOT_FOUND_ERROR, infobase));
            }

            if (!_metadataService.TryGetMetadataCache(infobase, out MetadataCache _, out string error))
            {
                return BadRequest(error);
            }

            if (!_metadataService.TryGetDbViewGenerator(infobase, out IDbViewGenerator generator, out error))
            {
                return BadRequest(error);
            }

            if (!string.IsNullOrWhiteSpace(schema))
            {
                generator.Options.Schema = schema;
            }

            List<string> views = generator.SelectViews(schema);

            string json = JsonSerializer.Serialize(views,
                new JsonSerializerOptions()
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                });

            return Content(json);
        }

        #region "SCRIPT VIEWS"

        [Produces("application/sql")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status206PartialContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{infobase}")]
        public ActionResult ScriptViews([FromRoute] string infobase, [FromQuery] string schema = null, [FromQuery] bool? codify = null)
        {
            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound(string.Format(INFOBASE_IS_NOT_FOUND_ERROR, infobase));
            }

            string fileName = $"view_{infobase}.sql"; // _{DateTime.Now:yyyy-MM-ddTHH:mm.ss}

            IFileInfo file = _fileProvider.GetFileInfo(fileName);

            if (file.Exists)
            {
                try
                {
                    System.IO.File.Delete(file.PhysicalPath);
                }
                catch (Exception exception)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ExceptionHelper.GetErrorMessage(exception));
                }
            }

            if (!_metadataService.TryGetMetadataCache(infobase, out MetadataCache cache, out string error))
            {
                return BadRequest(error);
            }

            if (!_metadataService.TryGetDbViewGenerator(infobase, out IDbViewGenerator generator, out error))
            {
                return BadRequest(error);
            }

            if (!string.IsNullOrWhiteSpace(schema))
            {
                generator.Options.Schema = schema;
            }

            if (codify.HasValue)
            {
                generator.Options.Codify = codify.Value;
            }
            
            generator.Options.OutputFile = file.PhysicalPath;

            try
            {
                using (StreamWriter writer = new(file.PhysicalPath, false, Encoding.UTF8))
                {
                    if (!generator.TryScriptViews(in cache, in writer, out error))
                    {
                        writer.WriteLine($"/* {error} */");
                        Response.StatusCode = StatusCodes.Status206PartialContent;
                    }
                }
            }
            catch (Exception exception)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ExceptionHelper.GetErrorMessage(exception));
            }

            byte[] content = System.IO.File.ReadAllBytes(file.PhysicalPath);

            if (content == null || content.Length == 0)
            {
                return NotFound($"Script file is empty: {fileName}");
            }

            try
            {
                System.IO.File.Delete(file.PhysicalPath);
            }
            catch
            {
                // do nothing
            }

            return File(content, "application/sql", fileName);

            //Stream stream = file.CreateReadStream();

            //if (stream == null)
            //{
            //    return NotFound(fileName);
            //}

            //return new FileStreamResult(stream, "application/sql") // application/octet-stream
            //{
            //    FileDownloadName = fileName
            //};
        }
        
        [Produces("text/plain")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status206PartialContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{infobase}/{type}/{name}")]
        public ActionResult ScriptView(
            [FromRoute] string infobase, [FromRoute] string type, [FromRoute] string name,
            [FromQuery] string schema = null, [FromQuery] bool? codify = null)
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

            MetadataObject metadata;

            try
            {
                metadata = cache.GetMetadataObject($"{type}.{name}");
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetErrorMessage(exception));
            }

            if (metadata is not ApplicationObject @object)
            {
                return NotFound();
            }

            string fileName = $"view_{infobase}_{type}_{name}.sql";

            IFileInfo file = _fileProvider.GetFileInfo(fileName);

            if (file.Exists)
            {
                try
                {
                    System.IO.File.Delete(file.PhysicalPath);
                }
                catch (Exception exception)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ExceptionHelper.GetErrorMessage(exception));
                }
            }

            if (!_metadataService.TryGetDbViewGenerator(infobase, out IDbViewGenerator generator, out error))
            {
                return BadRequest(error);
            }

            if (!string.IsNullOrWhiteSpace(schema))
            {
                generator.Options.Schema = schema;
            }

            if (codify.HasValue)
            {
                generator.Options.Codify = codify.Value;
            }
            
            try
            {
                using (StreamWriter writer = new(file.PhysicalPath, false, Encoding.UTF8))
                {
                    if (!generator.TryScriptView(in @object, in writer, out error))
                    {
                        writer.WriteLine($"/* {error} */");
                        Response.StatusCode = StatusCodes.Status206PartialContent;
                    }
                }
            }
            catch (Exception exception)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ExceptionHelper.GetErrorMessage(exception));
            }

            string content = System.IO.File.ReadAllText(file.PhysicalPath, Encoding.UTF8);

            if (string.IsNullOrWhiteSpace(content))
            {
                return NotFound($"Script file is empty: {fileName}");
            }

            try
            {
                System.IO.File.Delete(file.PhysicalPath);
            }
            catch
            {
                // do nothing
            }

            return Content(content, "text/plain", Encoding.UTF8);
        }

        #endregion

        #region "CREATE VIEWS"

        public sealed class CreateViewsResponse
        {
            public int Result { get; set; }
            public List<string> Errors { get; set; } = new();
        }

        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{infobase}")]
        public ActionResult CreateViews([FromRoute] string infobase, [FromBody] DbViewGeneratorOptions options)
        {
            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound(string.Format(INFOBASE_IS_NOT_FOUND_ERROR, infobase));
            }

            if (!_metadataService.TryGetMetadataCache(infobase, out MetadataCache cache, out string error))
            {
                return BadRequest(error);
            }

            if (!_metadataService.TryGetDbViewGenerator(infobase, out IDbViewGenerator generator, out error))
            {
                return BadRequest(error);
            }

            if (!string.IsNullOrWhiteSpace(options.Schema))
            {
                generator.Options.Schema = options.Schema;
            }

            if (options.Codify)
            {
                generator.Options.Codify = options.Codify;
            }

            _ = generator.TryCreateViews(in cache, out int result, out List<string> errors);

            CreateViewsResponse response = new()
            {
                Result = result,
                Errors = errors
            };

            Response.StatusCode = StatusCodes.Status201Created;

            string json = JsonSerializer.Serialize(response,
                new JsonSerializerOptions()
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                });

            return Content(json, "application/json; charset=utf-8", Encoding.UTF8);
        }

        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{infobase}/{type}/{name}")]
        public ActionResult CreateView(
            [FromRoute] string infobase, [FromRoute] string type, [FromRoute] string name,
            [FromBody] DbViewGeneratorOptions options)
        {
            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound(string.Format(INFOBASE_IS_NOT_FOUND_ERROR, infobase));
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

            MetadataObject metadata;

            try
            {
                metadata = cache.GetMetadataObject($"{type}.{name}");
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetErrorMessage(exception));
            }

            if (metadata is not ApplicationObject @object)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetDbViewGenerator(infobase, out IDbViewGenerator generator, out error))
            {
                return BadRequest(error);
            }

            if (!string.IsNullOrWhiteSpace(options.Schema))
            {
                generator.Options.Schema = options.Schema;
            }

            if (options.Codify)
            {
                generator.Options.Codify = options.Codify;
            }

            try
            {
                if (!generator.TryCreateView(in @object, out error))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, error);
                }
            }
            catch (Exception exception)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ExceptionHelper.GetErrorMessage(exception));
            }

            return Created("view", null);
        }

        #endregion

        #region "DELETE VIEWS"

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{infobase}")]
        public ActionResult DeleteViews([FromRoute] string infobase, [FromQuery] string schema = null)
        {
            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound(string.Format(INFOBASE_IS_NOT_FOUND_ERROR, infobase));
            }

            if (!_metadataService.TryGetDbViewGenerator(infobase, out IDbViewGenerator generator, out string error))
            {
                return BadRequest(error);
            }

            if (!string.IsNullOrWhiteSpace(schema))
            {
                generator.Options.Schema = schema;
            }

            try
            {
                _ = generator.DropViews();
            }
            catch (Exception exception)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ExceptionHelper.GetErrorMessage(exception));
            }

            return Ok();
        }

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{infobase}/{type}/{name}")]
        public ActionResult DeleteView(
            [FromRoute] string infobase, [FromRoute] string type, [FromRoute] string name, [FromBody] DbViewGeneratorOptions options)
        {
            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound(string.Format(INFOBASE_IS_NOT_FOUND_ERROR, infobase));
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

            MetadataObject metadata;

            try
            {
                metadata = cache.GetMetadataObject($"{type}.{name}");
            }
            catch (Exception exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ExceptionHelper.GetErrorMessage(exception));
            }

            if (metadata is not ApplicationObject @object)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetDbViewGenerator(infobase, out IDbViewGenerator generator, out error))
            {
                return BadRequest(error);
            }

            if (!string.IsNullOrWhiteSpace(options.Schema))
            {
                generator.Options.Schema = options.Schema;
            }

            if (options.Codify)
            {
                generator.Options.Codify = options.Codify;
            }

            try
            {
                generator.DropView(in @object);
            }
            catch (Exception exception)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ExceptionHelper.GetErrorMessage(exception));
            }

            return Ok();
        }

        #endregion
    }
}