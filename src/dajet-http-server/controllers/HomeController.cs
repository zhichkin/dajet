using DaJet.Data;
using DaJet.Flow;
using DaJet.Flow.Model;
using DaJet.Json;
using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using PipelineState = DaJet.Model.PipelineState;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("home")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public class HomeController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        static HomeController()
        {
            JsonOptions.Converters.Add(new DictionaryJsonConverter());
        }
        private readonly IDataSource _source;
        private readonly IDomainModel _domain;
        private readonly IPipelineManager _manager;
        private readonly IPipelineBuilder _builder;
        public HomeController(IDomainModel domain, IDataSource source, IPipelineManager manager, IPipelineBuilder builder)
        {
            _domain = domain;
            _source = source;
            _manager = manager;
            _builder = builder;
        }

        [HttpGet("{typeCode:int}")]
        public ActionResult Select([FromRoute] int typeCode)
        {
            Type type = _domain.GetEntityType(typeCode);

            if (type is null) { return NotFound(); }

            var list = _source.Select(typeCode);

            Type listType = typeof(List<>).MakeGenericType(type);

            string json = JsonSerializer.Serialize(list, listType, JsonOptions);

            return Content(json, "application/json", Encoding.UTF8);
        }
        
        [HttpGet("{typeCode:int}/{identity:guid}")]
        public ActionResult Select([FromRoute] int typeCode, [FromRoute] Guid identity)
        {
            Type type = _domain.GetEntityType(typeCode);

            if (type is null) { return NotFound(); }

            Entity entity = new(typeCode, identity);

            EntityObject record = _source.Select(entity);

            string json = JsonSerializer.Serialize(record, type, JsonOptions);

            return Content(json, "application/json", Encoding.UTF8);
        }

        [HttpGet("{typeCode:int}/{ownerType:int}/{identity:guid}")]
        public ActionResult Select([FromRoute] int typeCode, [FromRoute] int ownerType, [FromRoute] Guid identity)
        {
            Type type = _domain.GetEntityType(typeCode);

            var list = _source.Select(typeCode, new Entity(ownerType, identity));

            Type listType = typeof(List<>).MakeGenericType(type);

            string json = JsonSerializer.Serialize(list, listType, JsonOptions);

            return Content(json, "application/json", Encoding.UTF8);
        }

        [HttpPost("{typeCode:int}")] public async Task<ActionResult> Insert([FromRoute] int typeCode)
        {
            HttpRequest request = HttpContext.Request;

            if (request.ContentLength == 0)
            {
                return BadRequest();
            }

            Type type = _domain.GetEntityType(typeCode);

            EntityObject entity = await JsonSerializer.DeserializeAsync(request.Body, type, JsonOptions) as EntityObject;

            _source.Create(entity);

            return Created(type.FullName, entity.Identity); // return Conflict();
        }
        [HttpPut("{typeCode:int}")] public async Task<ActionResult> Update([FromRoute] int typeCode)
        {
            HttpRequest request = HttpContext.Request;

            if (request.ContentLength == 0)
            {
                return BadRequest();
            }

            Type type = _domain.GetEntityType(typeCode);

            EntityObject entity = await JsonSerializer.DeserializeAsync(request.Body, type, JsonOptions) as EntityObject;

            _source.Update(entity);

            return Ok(); // return Conflict();
        }
        [HttpDelete("{typeCode:int}/{identity:guid}")] public ActionResult Delete([FromRoute] int typeCode, [FromRoute] Guid identity)
        {
            Type type = _domain.GetEntityType(typeCode);

            if (type is null)
            {
                return BadRequest();
            }

            Entity entity = new(typeCode, identity);

            _source.Delete(entity);

            return Ok(); // return NotFound(); // return Conflict();
        }



        [HttpPost("query/{typeCode:int}")]
        public async Task<ActionResult> Query([FromRoute] int typeCode)
        {
            HttpRequest request = HttpContext.Request;

            if (request.ContentLength == 0)
            {
                return BadRequest();
            }

            Type type = _domain.GetEntityType(typeCode);

            var parameters = await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(request.Body, JsonOptions);

            if (type == typeof(PipelineState))
            {
                List<PipelineInfo> monitor = _manager.GetMonitorInfo();

                List<PipelineState> result = new();

                foreach (PipelineInfo info in monitor)
                {
                    result.Add(new PipelineState()
                    {
                        Uuid = info.Uuid,
                        Name = info.Name,
                        State = info.State.ToString(),
                        Status = string.IsNullOrEmpty(info.Status) ? "нет данных" : info.Status,
                        Start = info.Start,
                        Finish = info.Finish,
                        Activation = info.Activation.ToString()
                    });
                }

                return Content(JsonSerializer.Serialize(result, JsonOptions), "application/json", Encoding.UTF8);
            }

            if (!parameters.TryGetValue("TreeNode", out object treeNode) || treeNode is not TreeNodeRecord folder)
            {
                return BadRequest();
            }

            var list = _source.Select(folder.GetEntity());

            Type listType = typeof(List<>).MakeGenericType(type);

            string json = JsonSerializer.Serialize(list, listType, JsonOptions);

            return Content(json, "application/json", Encoding.UTF8);
        }

        

        [HttpGet("get-tree-node-full-name/{identity:guid}")]
        public ActionResult GetTreeNodeFullName([FromRoute] Guid identity)
        {
            int typeCode = _domain.GetTypeCode(typeof(TreeNodeRecord));

            if (typeCode == 0)
            {
                return NotFound(nameof(TreeNodeRecord));
            }

            Entity entity = new(typeCode, identity);

            EntityObject record = _source.Select(entity);

            if (record is not TreeNodeRecord node)
            {
                return NotFound(entity.ToString());
            }
            
            string name = "/" + node.Name;

            Entity parent = node.Parent;

            while (!parent.IsEmpty)
            {
                TreeNodeRecord ancestor = _source.Select(parent) as TreeNodeRecord;

                if (ancestor is not null)
                {
                    name = "/" + ancestor.Name + name;
                }
                else
                {
                    break;
                }

                parent = ancestor.Parent;
            }

            return Content(name, "text/plain", Encoding.UTF8);
        }

        [HttpGet("get-available-processors")]
        public ActionResult GetAvailableProcessors()
        {
            List<ProcessorInfo> result = new();
            List<PipelineBlock> blocks = _builder.GetPipelineBlocks();

            foreach (var block in blocks)
            {
                result.Add(new ProcessorInfo()
                {
                    Handler = block.Handler,
                    Message = block.Message
                });
            }

            string json = JsonSerializer.Serialize(result, JsonOptions);

            return Content(json, "application/json", Encoding.UTF8);
        }
    }
}