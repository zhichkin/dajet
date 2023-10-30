using DaJet.Data;
using DaJet.Flow;
using DaJet.Flow.Model;
using DaJet.Model;
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
        private readonly JsonSerializerOptions _settings = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        private readonly IDataSource _source;
        private readonly IDomainModel _domain;
        private readonly IPipelineManager _manager;
        private readonly IPipelineBuilder _builder;
        private readonly IPipelineOptionsProvider _options;
        public FlowController(
            IDomainModel domain, IDataSource source,
            IPipelineOptionsProvider options, IPipelineManager manager, IPipelineBuilder builder)
        {
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }
        [HttpGet("")] public ActionResult SelectPipelines()
        {
            List<PipelineInfo> list = _manager.GetMonitorInfo();

            string json = JsonSerializer.Serialize(list, _settings);

            return Content(json);
        }
        [HttpGet("blocks")] public ActionResult SelectPipelineBlocks()
        {
            List<PipelineBlock> list = _builder.GetPipelineBlocks();

            string json = JsonSerializer.Serialize(list, _settings);

            return Content(json);
        }
        [HttpGet("{pipeline:guid}")] public ActionResult SelectPipeline([FromRoute] Guid pipeline)
        {
            PipelineOptions entity = _options.Select(pipeline);

            if (entity is null) { return NotFound(); }

            foreach (OptionItem option in _builder.GetOptions(typeof(Pipeline)))
            {
                if (entity.Options.Where(item => item.Name == option.Name).FirstOrDefault() is null)
                {
                    entity.Options.Add(option);
                }
            }

            foreach (PipelineBlock block in entity.Blocks)
            {
                foreach (OptionItem option in _builder.GetOptions(block.Handler))
                {
                    if (block.Options.Where(item => item.Name == option.Name).FirstOrDefault() is null)
                    {
                        block.Options.Add(option);
                    }
                }
            }

            string json = JsonSerializer.Serialize(entity, _settings);

            return Content(json);
        }
        [HttpGet("options/{owner}")] public ActionResult SelectOwnerOptions([FromRoute] string owner)
        {
            List<OptionItem> list = _builder.GetOptions(owner);

            string json = JsonSerializer.Serialize(list, _settings);

            return Content(json);
        }

        [HttpPut("execute/{pipeline:guid}")] public ActionResult ExecutePipeline([FromRoute] Guid pipeline)
        {
            PipelineOptions entity = _options.Select(pipeline);

            if (entity is null) { return NotFound(); }

            _manager.ExecutePipeline(entity.Uuid);

            return Ok();
        }
        [HttpPut("dispose/{pipeline:guid}")] public ActionResult DisposePipeline([FromRoute] Guid pipeline)
        {
            PipelineOptions entity = _options.Select(pipeline);

            if (entity is null) { return NotFound(); }

            _manager.DisposePipeline(entity.Uuid);

            return Ok();
        }
        [HttpPut("restart/{pipeline:guid}")] public ActionResult ReStartPipeline([FromRoute] Guid pipeline)
        {
            PipelineOptions options = _options.Select(pipeline);

            if (options is null) { return NotFound(); }

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
            PipelineOptions options = _options.Select(pipeline);

            if (options is null) { return NotFound(); }

            try
            {
                _ = _builder.Build(options);
            }
            catch (Exception error)
            {
                return Problem(ExceptionHelper.GetErrorMessageAndStackTrace(error));
            }

            return Ok();
        }

        [HttpPost("")] public ActionResult Insert([FromBody] PipelineOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Name))
            {
                return BadRequest("Неверно указаны параметры!");
            }

            _ = _options.Insert(in options);

            int typeCode = _domain.GetTypeCode(typeof(TreeNodeRecord));

            if (typeCode > 0)
            {
                var list = _source.Select(typeCode, Entity.Undefined);

                if (list is List<TreeNodeRecord> nodes)
                {
                    TreeNodeRecord flowNode = nodes.Where(n => n.Name == "flow").FirstOrDefault();

                    if (flowNode is not null)
                    {
                        typeCode = _domain.GetTypeCode(typeof(PipelineRecord));

                        var entity = _source.Select(new Entity(typeCode, options.Uuid));

                        if (entity is PipelineRecord pipeline)
                        {
                            TreeNodeRecord treeNode = _domain.New<TreeNodeRecord>();

                            treeNode.Name = pipeline.Name;
                            treeNode.Value = pipeline.GetEntity();
                            treeNode.Parent = flowNode.GetEntity();
                            treeNode.IsFolder = false;

                            _source.Create(treeNode);
                        }
                    }
                }
            }
            
            return Created($"{options.Name}", $"{options.Uuid}");
        }
        [HttpPut("")] public async Task<ActionResult> Update([FromBody] PipelineOptions options)
        {
            PipelineOptions current = _options.Select(options.Uuid);

            if (current is null) { return NotFound(); }

            _ = _options.Update(in options);

            await _manager.ReStartPipeline(options.Uuid);

            return Ok();
        }
        [HttpDelete("{pipeline:guid}")] public async Task<ActionResult> Delete([FromRoute] Guid pipeline)
        {
            await _manager.DeletePipeline(pipeline); return Ok();
        }
    }
}