using DaJet.Data;
using DaJet.Http.DataMappers;
using DaJet.Http.Model;
using DaJet.Metadata;
using DaJet.Scripting;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("1ql")]
    public class QueryController : ControllerBase
    {
        private readonly InfoBaseDataMapper _mapper = new();
        private readonly IMetadataService _metadataService;
        public QueryController(IMetadataService metadataService)
        {
            _metadataService = metadataService;
        }
        [HttpGet("{infobase}")] public ContentResult Home([FromRoute] string infobase)
        {
            InfoBaseModel? record = _mapper.Select(infobase);

            if (record == null)
            {
                return new ContentResult()
                {
                    ContentType = "text/html",
                    StatusCode = (int)HttpStatusCode.NotFound,
                    Content = "<html><body>" + infobase + " is not found!</body></html>"
                };
            }

            string root = AppContext.BaseDirectory;
            string filePath = Path.Combine(root, "pages", "1ql.html");

            FileInfo info = new(filePath);

            if (!info.Exists)
            {
                return new ContentResult()
                {
                    ContentType = "text/html",
                    StatusCode = (int)HttpStatusCode.NotFound,
                    Content = "<html><body>1QL page is not found!</body></html>"
                };
            }

            string content = string.Empty;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }
            
            content = content.Replace("{InfoBaseName}", infobase);

            return new ContentResult()
            {
                ContentType = "text/html",
                StatusCode = (int)HttpStatusCode.OK,
                Content = content
            };
        }
        [HttpPost("execute")] public ActionResult Execute([FromBody] QueryModel query)
        {
            if (string.IsNullOrWhiteSpace(query.DbName) || string.IsNullOrWhiteSpace(query.Script))
            {
                return BadRequest();
            }

            InfoBaseModel? record = _mapper.Select(query.DbName);

            if (record == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataCache(query.DbName, out MetadataCache cache, out string error))
            {
                return BadRequest(error);
            }

            ScriptExecutor executor = new(cache);

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
        [HttpPost("prepare")] public ActionResult Generate([FromBody] QueryModel query)
        {
            if (string.IsNullOrWhiteSpace(query.DbName) || string.IsNullOrWhiteSpace(query.Script))
            {
                return BadRequest();
            }

            InfoBaseModel? record = _mapper.Select(query.DbName);

            if (record == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataCache(query.DbName, out MetadataCache cache, out string error))
            {
                return BadRequest(error);
            }

            ScriptExecutor executor = new(cache);

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
    }
}