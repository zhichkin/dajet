using DaJet.Data;
using DaJet.Http.Model;
using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("query")]
    public class QueryController : ControllerBase
    {
        private readonly IDataSource _source;
        private readonly ScriptDataMapper _scripts;
        private readonly IMetadataService _metadataService;
        public QueryController(IDataSource source, ScriptDataMapper scripts, IMetadataService metadataService)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        }
        [HttpPost("prepare")] public ActionResult Generate([FromBody] QueryModel query)
        {
            if (string.IsNullOrWhiteSpace(query.DbName) || string.IsNullOrWhiteSpace(query.Script))
            {
                return BadRequest();
            }

            InfoBaseRecord record = _source.Select<InfoBaseRecord>(query.DbName);

            if (record is null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataProvider(record.Identity.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            ScriptExecutor executor = new(provider, _metadataService, _source, _scripts);

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

            InfoBaseRecord record = _source.Select<InfoBaseRecord>(query.DbName);

            if (record is null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataProvider(record.Identity.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            ScriptExecutor executor = new(provider, _metadataService, _source, _scripts);

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

        [HttpPost("ddl")][Authorize]public ActionResult ExecuteNonQuery([FromBody] QueryModel query)
        {
            if (string.IsNullOrWhiteSpace(query.DbName) || string.IsNullOrWhiteSpace(query.Script))
            {
                return BadRequest();
            }

            InfoBaseRecord record = _source.Select<InfoBaseRecord>(query.DbName);

            if (record is null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataProvider(record.Identity.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            ScriptExecutor executor = new(provider, _metadataService, _source, _scripts);

            GeneratorResult result = new()
            {
                Success = true
            };

            try
            {
                executor.ExecuteNonQuery(query.Script);
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