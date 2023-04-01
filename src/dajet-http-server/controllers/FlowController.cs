using DaJet.Flow;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("flow")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public class FlowController : ControllerBase
    {
        private readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private readonly PipelineOptionsProvider _provider;
        public FlowController(PipelineOptionsProvider provider) { _provider = provider; }
        [HttpGet("")] public ActionResult Select()
        {
            List<PipelineOptions> list = _provider.Select();

            string json = JsonSerializer.Serialize(list, _options);

            return Content(json);
        }
        [HttpGet("{pipeline:guid}")] public ActionResult Select([FromRoute] string pipeline)
        {
            PipelineOptions options = _provider.Select(new Guid(pipeline));

            if (options is null) { return NotFound(); }

            string json = JsonSerializer.Serialize(options, _options);

            return Content(json);
        }
        [HttpPost("")] public ActionResult Insert([FromBody] PipelineOptions entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                return BadRequest("Неверно указаны параметры!");
            }

            _provider.Insert(entity);

            return Created($"{entity.Name}", $"{entity.Uuid}");
        }
        [HttpPut("")] public ActionResult Update([FromBody] PipelineOptions entity)
        {
            PipelineOptions options = _provider.Select(entity.Uuid);

            if (options is null) { return NotFound(); }

            _provider.Update(entity);

            return Ok();
        }
        [HttpDelete("{pipeline:guid}")] public ActionResult Delete([FromRoute] string pipeline)
        {
            PipelineOptions options = _provider.Select(new Guid(pipeline));

            if (options is null) { return NotFound(); }

            _provider.Delete(options);

            return Ok();
        }
    }
}