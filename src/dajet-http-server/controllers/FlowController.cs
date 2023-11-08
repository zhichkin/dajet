using DaJet.Data;
using DaJet.Flow;
using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
using System.Text;
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
        private readonly JsonSerializerOptions _settings = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private readonly IDataSource _source;
        private readonly IPipelineManager _manager;
        private readonly IAssemblyManager _resolver;
        public FlowController(IDataSource source, IPipelineManager manager, IAssemblyManager resolver)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }
        [HttpGet("")] public ActionResult SelectPipelines()
        {
            List<PipelineInfo> list = _manager.GetMonitorInfo();

            string json = JsonSerializer.Serialize(list, _settings);

            return Content(json);
        }
        [HttpGet("{pipeline:guid}")] public ActionResult SelectPipeline([FromRoute] Guid pipeline)
        {
            PipelineRecord record = _source.Select<PipelineRecord>(pipeline);

            string json = JsonSerializer.Serialize(record, _settings);

            return Content(json, "application/json", Encoding.UTF8);
        }
        [HttpGet("handlers")] public ActionResult GetAvailableHandlers()
        {
            List<PipelineBlock> list = _manager.GetAvailableHandlers();

            string json = JsonSerializer.Serialize(list, _settings);

            return Content(json);
        }
        [HttpGet("options/{owner}")] public ActionResult GetOwnerOptions([FromRoute] string owner)
        {
            Type type = _resolver.Resolve(owner);

            if (type is null)
            {
                return NotFound();
            }

            List<OptionItem> list = _manager.GetAvailableOptions(type);

            string json = JsonSerializer.Serialize(list, _settings);

            return Content(json);
        }

        [HttpPut("execute/{pipeline:guid}")] public ActionResult ExecutePipeline([FromRoute] Guid pipeline)
        {
            PipelineRecord record = _source.Select<PipelineRecord>(pipeline);

            if (record is null) { return NotFound(); }

            _manager.ExecutePipeline(pipeline);

            return Ok();
        }
        [HttpPut("dispose/{pipeline:guid}")] public ActionResult DisposePipeline([FromRoute] Guid pipeline)
        {
            PipelineRecord record = _source.Select<PipelineRecord>(pipeline);

            if (record is null) { return NotFound(); }

            _manager.DisposePipeline(pipeline);

            return Ok();
        }
        [HttpPut("restart/{pipeline:guid}")] public ActionResult ReStartPipeline([FromRoute] Guid pipeline)
        {
            PipelineRecord record = _source.Select<PipelineRecord>(pipeline);

            if (record is null) { return NotFound(); }

            try
            {
                _manager.ReStartPipeline(pipeline);
            }
            catch (Exception error)
            {
                return Problem(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }

            return Ok();
        }
        [HttpGet("validate/{pipeline:guid}")] public ActionResult ValidatePipeline([FromRoute] Guid pipeline)
        {
            PipelineRecord record = _source.Select<PipelineRecord>(pipeline);

            if (record is null) { return NotFound(); }

            try
            {
                _manager.ValidatePipeline(pipeline);
            }
            catch (Exception error)
            {
                return Problem(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }

            return Ok();
        }

        [HttpPost("")] public ActionResult Insert([FromBody] PipelineOptions options)
        {
            return NotFound();

            //if (string.IsNullOrWhiteSpace(options.Name))
            //{
            //    return BadRequest("Неверно указаны параметры!");
            //}

            //_ = _options.Insert(in options);

            //int typeCode = _domain.GetTypeCode(typeof(TreeNodeRecord));

            //if (typeCode > 0)
            //{
            //    var list = _source.Select(typeCode, Entity.Undefined);

            //    if (list is List<TreeNodeRecord> nodes)
            //    {
            //        TreeNodeRecord flowNode = nodes.Where(n => n.Name == "flow").FirstOrDefault();

            //        if (flowNode is not null)
            //        {
            //            typeCode = _domain.GetTypeCode(typeof(PipelineRecord));

            //            var entity = _source.Select(new Entity(typeCode, options.Uuid));

            //            if (entity is PipelineRecord pipeline)
            //            {
            //                TreeNodeRecord treeNode = _domain.New<TreeNodeRecord>();

            //                treeNode.Name = pipeline.Name;
            //                treeNode.Value = pipeline.GetEntity();
            //                treeNode.Parent = flowNode.GetEntity();
            //                treeNode.IsFolder = false;

            //                _source.Create(treeNode);
            //            }
            //        }
            //    }
            //}
            
            //return Created($"{options.Name}", $"{options.Uuid}");
        }
        [HttpPut("")] public ActionResult Update([FromBody] PipelineOptions options)
        {
            return NotFound();

            //PipelineOptions current = _options.Select(options.Uuid);

            //if (current is null) { return NotFound(); }

            //_ = _options.Update(in options);

            //await _manager.ReStartPipeline(options.Uuid);

            //return Ok();
        }
        [HttpDelete("{pipeline:guid}")] public ActionResult Delete([FromRoute] Guid pipeline)
        {
            return NotFound();

            //await _manager.DeletePipeline(pipeline); return Ok();
        }
    }
}