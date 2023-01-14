using DaJet.Data;
using DaJet.Http.DataMappers;
using DaJet.Http.Model;
using DaJet.Metadata;
using DaJet.Scripting;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("api")]
    public class ScriptController : ControllerBase
    {
        private readonly ScriptDataMapper _scripts = new();
        private readonly InfoBaseDataMapper _mapper = new();
        private readonly IMetadataService _metadataService;
        public ScriptController(IMetadataService metadataService)
        {
            _metadataService = metadataService;
        }
        [HttpGet("select/{infobase}")] public ActionResult Select([FromRoute] string infobase)
        {
            List<ScriptModel> list = _scripts.Select(infobase);

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

        [HttpGet("url/{uuid:guid}")] public ActionResult SelectScriptUrl([FromRoute] Guid uuid)
        {
            if (!_scripts.TrySelect(uuid, out ScriptModel script))
            {
                return NotFound();
            }

            string url = "/" + script.Name;
            string database = script.Owner;

            script = _scripts.SelectScript(script.Parent);

            while (script != null)
            {
                url = "/" + script.Name + url;
                script = _scripts.SelectScript(script.Parent);
            }

            url = "/api/" + database + url;

            return Content(url);
        }
        [HttpGet("{uuid:guid}")] public ActionResult SelectScript([FromRoute] Guid uuid)
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
        [HttpPost("")] public ActionResult InsertScript([FromBody] ScriptModel script)
        {
            string parentName;

            if (script.Parent == Guid.Empty)
            {
                parentName = string.Empty;
            }
            else if (_scripts.TrySelect(script.Parent, out ScriptModel parent))
            {
                parentName = $"/{parent.Name}";
            }
            else
            {
                return NotFound();
            }

            if (!_scripts.Insert(script))
            {
                return BadRequest();
            }

            return Created($"/{script.Owner}{parentName}/{script.Name}", $"{script.Uuid}");
        }
        [HttpPut("")] public ActionResult UpdateScript([FromBody] ScriptModel script)
        {
            if (!_scripts.Update(script))
            {
                return Conflict();
            }
            return Ok();
        }
        [HttpPut("name")] public ActionResult UpdateScriptName([FromBody] ScriptModel script)
        {
            if (!_scripts.UpdateName(script))
            {
                return Conflict();
            }
            return Ok();
        }
        [HttpDelete("{uuid:guid}")] public ActionResult DeleteScript([FromRoute] Guid uuid)
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

        [HttpPost("{infobase}/{**path}")] public async Task<ActionResult> ExecuteScript([FromRoute] string infobase, [FromRoute] string path)
        {
            HttpRequest request = HttpContext.Request;
            int contentLength = (int)request.ContentLength;

            byte[] buffer = new byte[contentLength];

            int bytesRead = await request.Body.ReadAsync(buffer, 0, contentLength);

            string content = Encoding.UTF8.GetString(buffer);

            ScriptModel script = SelectScriptByPath(infobase, path);

            if (script == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(script.Script))
            {
                return BadRequest();
            }

            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataCache(infobase, out MetadataCache cache, out string error))
            {
                return BadRequest(error);
            }

            ScriptExecutor executor = new(cache);

            //TODO: parse parameters from request body !!!

            //if (parameters != null)
            //{
            //    foreach (var parameter in parameters)
            //    {
            //        executor.Parameters.Add(parameter.Key, parameter.Value);
            //    }
            //}

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
        private ScriptModel SelectScriptByPath(string infobase, string path)
        {
            string[] segments = path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            int counter = 0;
            ScriptModel current = null;
            List<ScriptModel> list = _scripts.Select(infobase);

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
    }
}