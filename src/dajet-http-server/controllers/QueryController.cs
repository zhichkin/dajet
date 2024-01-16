using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Http.Model;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Model;
using DaJet.Scripting;
using DaJet.Scripting.Engine;
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
        private readonly IMetadataService _metadataService;
        public QueryController(IDataSource source, IMetadataService metadataService)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
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

            Dictionary<string, object> parameters = new();

            if (!ScriptProcessor.TryProcess(in provider, query.Script, in parameters, out TranspilerResult result, out error))
            {
                return BadRequest(error);
            }

            DataObject response = new();
            response.SetValue("Success", true);
            response.SetValue("Error", string.Empty);
            response.SetValue("Script", result.SqlScript);

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            options.Converters.Add(new DataObjectJsonConverter());

            string json = JsonSerializer.Serialize(response, options);

            return Content(json);
        }
        [HttpPost("execute")][Authorize]public ActionResult Execute([FromBody] QueryModel query)
        {
            if (string.IsNullOrWhiteSpace(query.DbName) || string.IsNullOrWhiteSpace(query.Script))
            {
                return BadRequest();
            }

            InfoBaseRecord database = _source.Select<InfoBaseRecord>(query.DbName);

            if (database is null)
            {
                return NotFound();
            }

            if (!_metadataService.TryGetMetadataProvider(database.Identity.ToString(), out IMetadataProvider provider, out string error))
            {
                return BadRequest(error);
            }

            List<Dictionary<string, object>> table = new();

            try
            {
                using (OneDbConnection connection = new(provider))
                {
                    connection.Open();

                    using (OneDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = query.Script;

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
            options.Converters.Add(new DictionaryJsonConverter());

            string json = JsonSerializer.Serialize(table, options);

            return Content(json);
        }
    }
}