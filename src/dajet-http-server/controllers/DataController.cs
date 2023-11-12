using DaJet.Data;
using DaJet.Flow;
using DaJet.Json;
using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Http.Controllers
{
    [ApiController][Route("data")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public class DataController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        static DataController()
        {
            JsonOptions.Converters.Add(new DataRecordJsonConverter());
            JsonOptions.Converters.Add(new DictionaryJsonConverter());
        }
        private readonly IDataSource _source;
        private readonly IDomainModel _domain;
        private readonly IPipelineManager _manager;
        public DataController(IDomainModel domain, IDataSource source, IPipelineManager manager)
        {
            _domain = domain;
            _source = source;
            _manager = manager;
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

        [HttpPost("query")] public async Task<ActionResult> Query()
        {
            HttpRequest request = HttpContext.Request;

            if (request.ContentLength == 0)
            {
                return BadRequest();
            }
            
            var parameters = await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(request.Body, JsonOptions);

            if (!parameters.TryGetValue("Query", out object parameter) || parameter is not string query)
            {
                return BadRequest();
            }

            _ = parameters.Remove("Query");

            List<IDataRecord> list = _source.Query(query, parameters);

            string json = JsonSerializer.Serialize(list, JsonOptions);

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
                return NotFound();
            }

            Entity entity = new(typeCode, identity);

            _source.Delete(entity);

            return Ok(); // return NotFound(); // return Conflict();
        }


        [HttpGet("get-tree-node-full-name/{identity:guid}")]
        public ActionResult GetTreeNodeFullName([FromRoute] Guid identity)
        {
            TreeNodeRecord node = _source.Select<TreeNodeRecord>(identity);

            if (node is null)
            {
                return NotFound(identity.ToString());
            }
            
            string name = "/" + node.Name;

            Entity parent = node.Parent;

            while (!parent.IsEmpty)
            {
                TreeNodeRecord ancestor = _source.Select<TreeNodeRecord>(parent);

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
    }
}