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
            List<HandlerModel> list = _manager.GetAvailableHandlers();

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

            List<OptionModel> list = _manager.GetAvailableOptions(type);

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

        [HttpPost("")] public ActionResult CreatePipeline([FromBody] PipelineModel model)
        {
            var nodes = _source.Query<TreeNodeRecord>(Entity.Undefined);

            TreeNodeRecord flowNode = nodes.Where(n => n.Name == "flow").FirstOrDefault();

            if (flowNode is null)
            {
                return NotFound("Узел сервиса \"flow\" не найден!");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest("Неверно указаны параметры!");
            }

            PipelineRecord pipeline = _source.Model.New<PipelineRecord>();
            pipeline.Name = model.Name;
            pipeline.Activation = model.Activation;
            _source.Create(pipeline);

            CreateOptions(pipeline.GetEntity(), model.Options);

            for (int ordinal = 0; ordinal < model.Handlers.Count; ordinal++)
            {
                HandlerModel item = model.Handlers[ordinal];

                HandlerRecord handler = _source.Model.New<HandlerRecord>();
                handler.Pipeline = pipeline.GetEntity();
                handler.Ordinal = ordinal;
                handler.Handler = item.Handler;
                handler.Message = item.Message;
                _source.Create(handler);

                CreateOptions(handler.GetEntity(), item.Options);
            }

            TreeNodeRecord treeNode = _source.Model.New<TreeNodeRecord>();
            treeNode.Name = pipeline.Name;
            treeNode.Value = pipeline.GetEntity();
            treeNode.Parent = flowNode.GetEntity();
            treeNode.IsFolder = false;
            _source.Create(treeNode);

            return Created($"{pipeline.Name}", $"{pipeline.Identity}");
        }
        private void CreateOptions(Entity owner, List<OptionModel> options)
        {
            foreach (OptionModel option in options)
            {
                OptionRecord record = _source.Model.New<OptionRecord>();

                record.Owner = owner;
                record.Name = option.Name;
                record.Type = option.Type;
                record.Value = option.Value;
                
                _source.Create(record);
            }
        }
        [HttpDelete("{pipeline:guid}")] public async Task<ActionResult> DeletePipeline([FromRoute] Guid pipeline)
        {
            await _manager.DeletePipeline(pipeline);
            
            return Ok();
        }
    }
}