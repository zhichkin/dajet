using DaJet.Json;
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

        private static List<TreeNodeRecord> _nodes = new()
        {
            new()
            {
                Parent = new Entity(10, Guid.NewGuid()),
                Name = "Node 1",
                Value = new Entity(10, Guid.NewGuid())
            },
            new()
            {
                Parent = new Entity(10, Guid.NewGuid()),
                Name = "Node 2",
                Value = new Entity(10, Guid.NewGuid())
            },
            new()
            {
                Parent = new Entity(10, Guid.NewGuid()),
                Name = "Привет, мир!",
                Value = new Entity(10, Guid.NewGuid())
            }
        };
        private readonly IDomainModel _domain;
        public HomeController(IDomainModel domain)
        {
            _domain = domain;
        }
        [HttpGet("")] public ActionResult SelectTreeNodes()
        {
            return NotFound();
        }
        
        [HttpGet("{typeCode:int}/{uuid:guid}")]
        public ActionResult Select([FromRoute] int typeCode, [FromRoute] Guid uuid)
        {
            TreeNodeRecord node = _nodes.Where(e => e.Value.Identity == uuid).FirstOrDefault();

            string json = JsonSerializer.Serialize(node, JsonOptions);

            return Content(json, "application/json", Encoding.UTF8);
        }

        [HttpGet("{typeCode:int}/{propertyName}/{value}")]
        public ActionResult Select([FromRoute] int typeCode, [FromRoute] string propertyName, [FromRoute] string value)
        {
            if (!Entity.TryParse(value, out Entity entity))
            {
                return BadRequest();
            }
            
            string json = JsonSerializer.Serialize(_nodes, JsonOptions);

            return Content(json, "application/json", Encoding.UTF8);
        }
    }
}