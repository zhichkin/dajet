using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
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
                Name = "Node 3",
                Value = new Entity(10, Guid.NewGuid())
            }
        };
        private readonly IDomainModel _domain;
        public HomeController(IDomainModel domain)
        {
            _domain = domain;
        }
        [HttpGet("")] public ActionResult<IEnumerable<TreeNodeRecord>> SelectTreeNodes()
        {
            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            //options.Converters.Add(new EntityJsonConverter(_domain));

            foreach (TreeNodeRecord record in _nodes)
            {
                record.MarkAsOriginal();
            }

            string json = JsonSerializer.Serialize(_nodes, options);

            return Content(json); // return _nodes;
        }
        
        [HttpGet("{type}/{uuid:guid}")]
        public ActionResult<TreeNodeRecord> Select([FromRoute] string type, [FromRoute] Guid uuid)
        {
            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            //options.Converters.Add(new EntityJsonConverter(_domain));

            TreeNodeRecord node = _nodes.Where(e => e.Value.Identity == uuid).FirstOrDefault();

            //TreeNodeRecord value = node.Value as TreeNodeRecord;

            string json = JsonSerializer.Serialize(node, options);

            return Content(json);
        }
    }
}