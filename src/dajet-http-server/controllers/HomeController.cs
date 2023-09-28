using DaJet.Data;
using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

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
        private readonly IDataSource _source;
        private readonly IDomainModel _domain;
        public HomeController(IDomainModel domain, IDataSource source)
        {
            _domain = domain;
            _source = source;
        }
        [HttpGet("")] public ActionResult Select()
        {
            int typeCode = _domain.GetTypeCode(typeof(TreeNodeRecord));

            if (typeCode == 0)
            {
                return NotFound(nameof(TreeNodeRecord));
            }

            var list = _source.Select(typeCode, "parent", Entity.Undefined);

            Type listType = typeof(List<>).MakeGenericType(typeof(TreeNodeRecord));

            string json = JsonSerializer.Serialize(list, listType, JsonOptions);

            return Content(json, "application/json", Encoding.UTF8);
        }
        
        [HttpGet("{typeCode:int}/{identity:guid}")]
        public ActionResult Select([FromRoute] int typeCode, [FromRoute] Guid identity)
        {
            Type type = _domain.GetEntityType(typeCode);

            Entity entity = new(typeCode, identity);

            EntityObject record = _source.Select(entity);

            string json = JsonSerializer.Serialize(record, type, JsonOptions);

            return Content(json, "application/json", Encoding.UTF8);
        }

        [HttpGet("{typeCode:int}/{propertyName}/{value}")]
        public ActionResult Select([FromRoute] int typeCode, [FromRoute] string propertyName, [FromRoute] string value)
        {
            Type type = _domain.GetEntityType(typeCode);

            if (!Entity.TryParse(value, out Entity entity))
            {
                return BadRequest();
            }

            var list = _source.Select(typeCode, propertyName, entity);

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
    }
}