using DaJet.Data;
using DaJet.Http.DataMappers;
using DaJet.Http.Model;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting;
using Microsoft.AspNetCore.Mvc;
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
        [HttpGet("{infobase}")] public ActionResult Select([FromRoute] string infobase)
        {
            InfoBaseModel record = _mapper.Select(infobase);

            if (record == null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetInfoBase(infobase, out InfoBase entity, out string error))
            {
                return BadRequest(error);
            }

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

        [HttpPost("{infobase}/{**path}")] public ActionResult Insert([FromRoute] string infobase, [FromRoute] string path, [FromBody] ScriptModel script)
        {
            if (string.IsNullOrEmpty(path))
            {
                script.Owner = infobase;
                script.Parent = Guid.Empty;
            }
            else
            {
                string[] segments = path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                int counter = 0;
                ScriptModel parent = null;
                List<ScriptModel> list = _scripts.Select(infobase);

                foreach (string segment in segments)
                {
                    parent = list.Where(item => item.Name == segment).FirstOrDefault();

                    if (parent == null) { break; }
                    
                    counter++;

                    if (counter < segments.Length)
                    {
                        list = _scripts.Select(parent);
                    }
                }

                if (counter == segments.Length && parent != null)
                {
                    script.Owner = infobase;
                    script.Parent = parent.Uuid;
                }
            }

            if (string.IsNullOrEmpty(script.Owner))
            {
                return BadRequest($"Не найден родитель для скрипта!");
            }

            if (!_scripts.Insert(script))
            {
                return BadRequest($"Ошибка создания скрипта.");
            }

            return Created($"/api/{infobase}/{path}/{script.Name}", $"{script.Uuid}");
        }
        [HttpPut("{infobase}/{**path}")] public ActionResult Update([FromRoute] string infobase, [FromRoute] string path, [FromBody] ScriptModel script)
        {
            script.Owner = infobase;
            script.Parent = Guid.Empty; // TODO !!!

            if (!_scripts.Update(script))
            {
                return Conflict();
            }

            return Ok();
        }
        [HttpDelete("{infobase}/{uuid}")] public ActionResult Delete([FromRoute] string infobase, [FromRoute] string uuid)
        {
            if (!Guid.TryParse(uuid, out Guid _))
            {
                return BadRequest();
            }

            if (!_scripts.TrySelect(in uuid, out ScriptModel script))
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


        [HttpPost("execute")] public ActionResult Execute([FromBody] QueryModel query)
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
    }
}