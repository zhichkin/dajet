using DaJet.Metadata;
using DaJet.Metadata.Services;
using DaJet.Options;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("db/schema")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public class DbSchemaController : ControllerBase
    {
        private const string INFOBASE_IS_NOT_FOUND_ERROR = "InfoBase [{0}] is not found. Try register it with the /md service first.";

        private readonly InfoBaseDataMapper _mapper;
        private readonly IMetadataService _metadataService;
        public DbSchemaController(InfoBaseDataMapper mapper, IMetadataService metadataService)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        }
        [HttpGet("{infobase}")] public ActionResult Select([FromRoute] string infobase)
        {
            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound(string.Format(INFOBASE_IS_NOT_FOUND_ERROR, infobase));
            }

            if (!_metadataService.TryGetDbViewGenerator(record.Uuid.ToString(), out IDbViewGenerator generator, out string error))
            {
                return BadRequest(error);
            }

            List<string> schemas = generator.SelectSchemas();

            string json = JsonSerializer.Serialize(schemas,
                new JsonSerializerOptions()
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                });

            return Content(json);
        }
        [HttpPost("{infobase}/{schema}")] public ActionResult Create([FromRoute] string infobase, [FromRoute] string schema)
        {
            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound(string.Format(INFOBASE_IS_NOT_FOUND_ERROR, infobase));
            }

            if (!_metadataService.TryGetDbViewGenerator(record.Uuid.ToString(), out IDbViewGenerator generator, out string error))
            {
                return BadRequest(error);
            }

            try
            {
                generator.CreateSchema(schema);
            }
            catch (Exception exception)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ExceptionHelper.GetErrorMessage(exception));
            }

            return Created($"schema", null);
        }
        [HttpDelete("{infobase}/{schema}")] public ActionResult Delete([FromRoute] string infobase, [FromRoute] string schema)
        {
            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound(string.Format(INFOBASE_IS_NOT_FOUND_ERROR, infobase));
            }

            if (!_metadataService.TryGetDbViewGenerator(record.Uuid.ToString(), out IDbViewGenerator generator, out string error))
            {
                return BadRequest(error);
            }

            try
            {
                generator.DropSchema(schema);
            }
            catch (Exception exception)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ExceptionHelper.GetErrorMessage(exception));
            }

            return Ok();
        }
    }
}