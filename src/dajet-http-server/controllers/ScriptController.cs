using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("api")] public class ScriptController : ControllerBase
    {
        private readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private readonly IDataSource _source;
        private readonly ScriptDataMapper _scripts;
        private readonly IMetadataService _metadataService;
        public ScriptController(IDataSource source, ScriptDataMapper scripts, IMetadataService metadataService)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        }
        [HttpGet("select/{infobase}")] public ActionResult Select([FromRoute] string infobase)
        {
            InfoBaseRecord database = _source.Select<InfoBaseRecord>(infobase);

            if (database is null) { return NotFound(); }

            List<ScriptRecord> list = _scripts.Select(database.Identity);

            foreach (ScriptRecord parent in list)
            {
                _scripts.GetScriptChildren(parent);
            }

            string json = JsonSerializer.Serialize(list, JsonOptions);

            return Content(json);
        }
        [HttpGet("{infobase}/{**path}")] public ActionResult Select([FromRoute] string infobase, [FromRoute] string path)
        {
            InfoBaseRecord database = _source.Select<InfoBaseRecord>(infobase);
            
            if (database is null)
            {
                return NotFound();
            }

            ScriptRecord script = _scripts.SelectScriptByPath(database.Identity, path);
            
            if (script is null)
            {
                return NotFound();
            }

            string json = JsonSerializer.Serialize(script, JsonOptions);

            return Content(json);
        }
        [HttpGet("url/{uuid:guid}")] public ActionResult SelectScriptUrl([FromRoute] Guid uuid)
        {
            if (!_scripts.TrySelect(uuid, out ScriptRecord script))
            {
                return NotFound();
            }

            InfoBaseRecord database = _source.Select<InfoBaseRecord>(script.Owner);

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
        [HttpGet("{uuid:guid}")] public ActionResult SelectScript([FromRoute] Guid uuid)
        {
            if (!_scripts.TrySelect(uuid, out ScriptRecord script))
            {
                return NotFound();
            }

            string json = JsonSerializer.Serialize(script, JsonOptions);

            return Content(json);
        }
        [HttpPost("")] public ActionResult InsertScript([FromBody] ScriptRecord script)
        {
            if (script.Parent != Guid.Empty && !_scripts.TrySelect(script.Parent, out ScriptRecord _))
            {
                return NotFound();
            }

            if (!_scripts.Insert(script))
            {
                return BadRequest();
            }

            return Created($"{script.Name}", $"{script.Uuid}");
        }
        [HttpPut("")] public ActionResult UpdateScript([FromBody] ScriptRecord script)
        {
            if (!_scripts.Update(script))
            {
                return Conflict();
            }
            return Ok();
        }
        [HttpPut("name")] public ActionResult UpdateScriptName([FromBody] ScriptRecord script)
        {
            if (!_scripts.UpdateName(script))
            {
                return Conflict();
            }
            return Ok();
        }
        [HttpDelete("{uuid:guid}")] public ActionResult DeleteScript([FromRoute] Guid uuid)
        {
            if (!_scripts.TrySelect(uuid, out ScriptRecord script))
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
        private void DeleteScriptFolder(ScriptRecord script)
        {
            List<ScriptRecord> children = _scripts.Select(script);

            foreach (ScriptRecord child in children)
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
            InfoBaseRecord database = _source.Select<InfoBaseRecord>(infobase);
            if (database is null) { return NotFound(); }

            ScriptRecord script = _scripts.SelectScriptByPath(database.Identity, path);
            if (script is null) { return NotFound(); }

            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(script.Script))
            {
                return BadRequest();
            }

            if (!_metadataService.TryGetMetadataProvider(database.Identity.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            ScriptExecutor executor = new(provider, _metadataService, _source, _scripts);

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
                List<List<Dictionary<string, object>>> batch = executor.Execute(script.Script);

                if (batch.Count > 0)
                {
                    List<Dictionary<string, object>> table = batch[0];

                    foreach (Dictionary<string, object> record in table)
                    {
                        foreach (var column in record)
                        {
                            if (column.Value is Entity value)
                            {
                                record[column.Key] = value.ToString();
                            }
                        }
                    }

                    result = table;
                }
            }
            catch (Exception exception)
            {
                return BadRequest(ExceptionHelper.GetErrorMessage(exception));
            }

            string json = JsonSerializer.Serialize(result, JsonOptions);

            return Content(json);
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