using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Http.Server;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Model;
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
        private readonly IMetadataService _metadataService;
        public ScriptController(IDataSource source, IMetadataService metadataService)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));

            JsonOptions.Converters.Add(new DictionaryJsonConverter());
        }
        [HttpGet("select/{infobase}")] public ActionResult Select([FromRoute] string infobase)
        {
            InfoBaseRecord database = _source.Select<InfoBaseRecord>(infobase);

            if (database is null) { return NotFound(); }

            IEnumerable<ScriptRecord> list = _source.Query<ScriptRecord>(database.GetEntity());

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

            ScriptRecord script = _source.Select<ScriptRecord>(database.Name + "/" + path);
            
            if (script is null)
            {
                return NotFound();
            }

            string json = JsonSerializer.Serialize(script, JsonOptions);

            return Content(json);
        }
        [HttpGet("url/{uuid:guid}")] public ActionResult SelectScriptUrl([FromRoute] Guid uuid)
        {
            ScriptRecord script = _source.Select<ScriptRecord>(uuid);

            if (script is null)
            {
                return NotFound();
            }

            InfoBaseRecord database = _source.Select<InfoBaseRecord>(script.Owner);

            if (database is null) { return NotFound(); }

            string url = "/" + script.Name;

            script = _source.Select<ScriptRecord>(script.Parent);

            while (script is not null)
            {
                url = "/" + script.Name + url;
                script = _source.Select<ScriptRecord>(script.Parent);
            }

            url = "/api/" + database.Name + url;

            return Content(url);
        }
        [HttpGet("{uuid:guid}")] public ActionResult SelectScript([FromRoute] Guid uuid)
        {
            ScriptRecord script = _source.Select<ScriptRecord>(uuid);

            if (script is null)
            {
                return NotFound();
            }

            string json = JsonSerializer.Serialize(script, JsonOptions);

            return Content(json);
        }
        [HttpPost("")] public ActionResult InsertScript([FromBody] ScriptRecord script)
        {
            _source.Create(script);

            return Created($"{script.Name}", $"{script.Identity}");
        }
        [HttpPut("")] public ActionResult UpdateScript([FromBody] ScriptRecord script)
        {
            _source.Update(script); return Ok();
        }
        [HttpPut("name")] public ActionResult UpdateScriptName([FromBody] ScriptRecord script)
        {
            _source.Update(script); return Ok(); // TODO: update name only !?
        }
        [HttpDelete("{uuid:guid}")] public ActionResult DeleteScript([FromRoute] Guid uuid)
        {
            _source.Delete<ScriptRecord>(uuid); return Ok();
        }
        
        [HttpPost("{infobase}/{**path}")]
        public async Task<ActionResult> ExecuteScript([FromRoute] string infobase, [FromRoute] string path)
        {
            InfoBaseRecord database = _source.Select<InfoBaseRecord>(infobase);
            if (database is null) { return NotFound(); }

            ScriptRecord script = _source.Select<ScriptRecord>(database.Name + "/" + path);
            if (script is null) { return NotFound(); }

            if (string.IsNullOrWhiteSpace(infobase) || string.IsNullOrWhiteSpace(script.Script))
            {
                return BadRequest();
            }

            if (!_metadataService.TryGetOrCreate(in database, out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            Dictionary<string, object> parameters = await HttpContext.Request.GetParametersFromBody();

            List<Dictionary<string, object>> table = new();

            using (OneDbConnection connection = new(provider))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = script.Script;

                    if (parameters is not null)
                    {
                        foreach (var parameter in parameters)
                        {
                            command.Parameters.Add(parameter.Key, parameter.Value);
                        }
                    }

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        //do
                        //{
                        while (reader.Read())
                        {
                            Dictionary<string, object> record = reader.Map();

                            table.Add(record);
                        }
                        //}
                        //while (reader.NextResult()); //TODO: multiple results

                        reader.Close();
                    }
                }
            }

            string json = JsonSerializer.Serialize(table, JsonOptions);

            return Content(json);
        }
    }
}