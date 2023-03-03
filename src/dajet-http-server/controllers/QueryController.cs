using DaJet.Data;
using DaJet.Http.DataMappers;
using DaJet.Http.Model;
using DaJet.Metadata;
using DaJet.Scripting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("djql")]
    public class QueryController : ControllerBase
    {
        private readonly InfoBaseDataMapper _mapper = new();
        private readonly IMetadataService _metadataService;
        public QueryController(IMetadataService metadataService)
        {
            _metadataService = metadataService;
        }
        [HttpPost("prepare")] public ActionResult Generate([FromBody] QueryModel query)
        {
            if (string.IsNullOrWhiteSpace(query.DbName) || string.IsNullOrWhiteSpace(query.Script))
            {
                return BadRequest();
            }

            InfoBaseModel record = _mapper.Select(query.DbName);

            if (record == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataProvider(record.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            ScriptExecutor executor = new(provider);

            GeneratorResult result;

            try
            {
                result = executor.PrepareScript(query.Script);
            }
            catch (Exception exception)
            {
                return BadRequest(ExceptionHelper.GetErrorMessage(exception));
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            string json = JsonSerializer.Serialize(result, options);

            return Content(json);
        }
        [HttpPost("execute")][Authorize]public ActionResult Execute([FromBody] QueryModel query)
        {
            if (string.IsNullOrWhiteSpace(query.DbName) || string.IsNullOrWhiteSpace(query.Script))
            {
                return BadRequest();
            }

            InfoBaseModel record = _mapper.Select(query.DbName);

            if (record == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataProvider(record.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            ScriptExecutor executor = new(provider);

            List<Dictionary<string, object>> result = new();

            try
            {
                foreach (var entity in executor.ExecuteReader(query.Script))
                {
                    foreach (var item in entity)
                    {
                        if (item.Value is Entity value)
                        {
                            entity[item.Key] = value.ToString();
                        }
                    }

                    result.Add(entity);
                }
            }
            catch (Exception exception)
            {
                return BadRequest(ExceptionHelper.GetErrorMessage(exception));
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            string json = JsonSerializer.Serialize(result, options);

            return Content(json);
        }
    }
}