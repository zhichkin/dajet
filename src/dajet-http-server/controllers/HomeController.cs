using DaJet.Json;
using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
using System.Security;
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
            new(null)
            {
                Parent = new(null) { Name = "root" },
                Name = "Node 1",
                Value = new TreeNodeRecord(null) { Name = "Pipeline" }
            },
            new(null)
            {
                Parent = new(null) { Name = "root" },
                Name = "Node 2",
                Value = new TreeNodeRecord(null) { Name = "Block" }
            },
            new(null)
            {
                Parent = new(null) { Name = "Parent 1" },
                Name = "Node 3",
                Value = new TreeNodeRecord(null) { Name = "Pipeline Block 123" }
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

            options.Converters.Add(new EntityJsonConverter(_domain));
            
            string json = JsonSerializer.Serialize(_nodes, options);

            return Content(json);
        }
        [HttpGet("{type}/{uuid:guid}")]
        public ActionResult<TreeNodeRecord> Select([FromRoute] string type, [FromRoute] Guid uuid)
        {
            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            options.Converters.Add(new EntityJsonConverter(_domain));

            TreeNodeRecord node = _nodes.Where(e => e.Value.Identity == uuid).FirstOrDefault();

            TreeNodeRecord value = node.Value as TreeNodeRecord;

            string json = JsonSerializer.Serialize(value, options);

            return Content(json);
        }
    }
}