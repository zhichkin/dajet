using DaJet.Data;
using DaJet.Http.DataMappers;
using DaJet.Http.Model;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Scripting;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController]
    [Route("api")]
    public class ScriptController : ControllerBase
    {
        private readonly ScriptDataMapper _scripts = new();
        private readonly InfoBaseDataMapper _mapper = new();
        private readonly IMetadataService _metadataService;
        public ScriptController(IMetadataService metadataService)
        {
            _metadataService = metadataService;
        }
        [HttpGet("select/{infobase}")]
        public ActionResult Select([FromRoute] string infobase)
        {
            InfoBaseModel database = _mapper.Select(infobase);
            if (database is null) { return NotFound(); }

            List<ScriptModel> list = _scripts.Select(database.Uuid);

            foreach (ScriptModel parent in list)
            {
                GetScriptChildren(parent);
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            string json = JsonSerializer.Serialize(list, options);

            return Content(json);
        }
        private void GetScriptChildren(ScriptModel parent)
        {
            List<ScriptModel> list = _scripts.Select(parent);

            if (list.Count == 0)
            {
                return;
            }

            parent.Children.AddRange(list);

            foreach (ScriptModel child in parent.Children)
            {
                GetScriptChildren(child);
            }
        }

        [HttpGet("url/{uuid:guid}")]
        public ActionResult SelectScriptUrl([FromRoute] Guid uuid)
        {
            if (!_scripts.TrySelect(uuid, out ScriptModel script))
            {
                return NotFound();
            }

            InfoBaseModel database = _mapper.Select(script.Owner);
            if (database is null) { return NotFound(); }

            string url = "/" + script.Name;

            script = _scripts.SelectScript(script.Parent);

            while (script != null)
            {
                url = "/" + script.Name + url;
                script = _scripts.SelectScript(script.Parent);
            }

            url = "/api/" + database.Name + url;

            return Content(url);
        }
        [HttpGet("{uuid:guid}")]
        public ActionResult SelectScript([FromRoute] Guid uuid)
        {
            if (!_scripts.TrySelect(uuid, out ScriptModel script))
            {
                return NotFound();
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            string json = JsonSerializer.Serialize(script, options);

            return Content(json);
        }
        [HttpPost("")]
        public ActionResult InsertScript([FromBody] ScriptModel script)
        {
            if (script.Parent != Guid.Empty && !_scripts.TrySelect(script.Parent, out ScriptModel _))
            {
                return NotFound();
            }

            if (!_scripts.Insert(script))
            {
                return BadRequest();
            }

            return Created($"{script.Name}", $"{script.Uuid}");
        }
        [HttpPut("")]
        public ActionResult UpdateScript([FromBody] ScriptModel script)
        {
            if (!_scripts.Update(script))
            {
                return Conflict();
            }
            return Ok();
        }
        [HttpPut("name")]
        public ActionResult UpdateScriptName([FromBody] ScriptModel script)
        {
            if (!_scripts.UpdateName(script))
            {
                return Conflict();
            }
            return Ok();
        }
        [HttpDelete("{uuid:guid}")]
        public ActionResult DeleteScript([FromRoute] Guid uuid)
        {
            if (!_scripts.TrySelect(uuid, out ScriptModel script))
            {
                return NotFound();
            }

            if (script.IsFolder)
            {
                DeleteScriptFolder(script);
            }
            else if (!_scripts.Delete(script))
            {
                return Conflict();
            }

            return Ok();
        }
        private void DeleteScriptFolder(ScriptModel script)
        {
            List<ScriptModel> children = _scripts.Select(script);

            foreach (ScriptModel child in children)
            {
                if (child.IsFolder)
                {
                    DeleteScriptFolder(child);
                }
                else
                {
                    _scripts.Delete(child);
                }
            }

            _scripts.Delete(script);
        }

        [HttpPost("{infobase}/{**path}")]
        public async Task<ActionResult> ExecuteScript([FromRoute] string infobase, [FromRoute] string path)
        {
            InfoBaseModel database = _mapper.Select(infobase);
            if (database is null) { return NotFound(); }

            ScriptModel script = SelectScriptByPath(database.Uuid, path);
            if (script is null) { return NotFound(); }

            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(script.Script))
            {
                return BadRequest();
            }

            if (!_metadataService.TryGetMetadataProvider(database.Uuid.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            ScriptExecutor executor = new(provider);

            Dictionary<string, object> parameters = await ParseScriptParametersFromBody();

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    executor.Parameters.Add(parameter.Key, parameter.Value);
                }
            }

            List<Dictionary<string, object>> result = new();
            try
            {
                foreach (var entity in executor.ExecuteReader(script.Script))
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
        private ScriptModel SelectScriptByPath(Guid database, string path)
        {
            string[] segments = path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            int counter = 0;
            ScriptModel current = null;
            List<ScriptModel> list = _scripts.Select(database);

            foreach (string segment in segments)
            {
                current = list.Where(item => item.Name == segment).FirstOrDefault();

                if (current == null) { break; }

                counter++;

                if (counter < segments.Length)
                {
                    list = _scripts.Select(current);
                }
            }

            if (counter == segments.Length && current != null)
            {
                if (_scripts.TrySelect(current.Uuid, out ScriptModel script))
                {
                    return script;
                }
                else
                {
                    return null;
                }
            }

            return null; // not found
        }
        private async Task<Dictionary<string, object>> ParseScriptParametersFromBody()
        {
            HttpRequest request = HttpContext.Request;

            if (request.ContentLength == 0)
            {
                return null;
            }

            JsonSerializerOptions options = new()
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                Converters =
                {
                    new ScriptParametersJsonConverter()
                }
            };

            return await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(request.Body, options);
        }
    }
}