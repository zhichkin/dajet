using DaJet.Flow;
using DaJet.Flow.Model;
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
        private readonly IPipelineManager _manager;
        private readonly IPipelineBuilder _builder;
        public FlowController(IPipelineManager manager, IPipelineBuilder builder)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }
        [HttpGet("")] public ActionResult Select()
        {
            List<PipelineInfo> list = _manager.Select();

            string json = JsonSerializer.Serialize(list, _options);

            return Content(json);
        }
        [HttpGet("{pipeline:guid}")] public ActionResult Select([FromRoute] Guid pipeline)
        {
            PipelineOptions options = _manager.Select(pipeline);

            if (options is null) { return NotFound(); }

            string json = JsonSerializer.Serialize(options, _options);

            return Content(json);
        }
        [HttpGet("options/{owner}")] public ActionResult SelectOptions([FromRoute] string owner)
        {
            List<OptionInfo> list = _builder.GetOptions(owner);

            string json = JsonSerializer.Serialize(list, _options);

            return Content(json);
        }
        [HttpGet("blocks")] public ActionResult SelectPipelineBlocks()
        {
            List<PipelineBlock> list = _builder.GetPipelineBlocks();

            string json = JsonSerializer.Serialize(list, _options);

            return Content(json);
        }

        [HttpPost("")] public ActionResult Insert([FromBody] PipelineOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Name))
            {
                return BadRequest("Неверно указаны параметры!");
            }

            _ = _manager.Insert(in options);

            return Created($"{options.Name}", $"{options.Uuid}");
        }
        [HttpPut("")] public ActionResult Update([FromBody] PipelineOptions options)
        {
            PipelineOptions current = _manager.Select(options.Uuid);

            if (current is null) { return NotFound(); }

            _ = _manager.Update(in options);

            return Ok();
        }
        [HttpDelete("{pipeline:guid}")] public ActionResult Delete([FromRoute] Guid pipeline)
        {
            PipelineOptions options = _manager.Select(pipeline);

            if (options is null) { return NotFound(); }

            _ = _manager.Delete(in options);

            return Ok();
        }
    }
}