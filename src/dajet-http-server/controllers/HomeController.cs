using DaJet.Json;
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
        private readonly IDataSource _source;
        private readonly IDomainModel _domain;
        public HomeController(IDomainModel domain, IDataSource source)
        {
            _domain = domain;
            _source = source;
        }
        [HttpGet()] public ActionResult Home()
        {
            throw new NotImplementedException();
        }
        [HttpPost("select")] public async Task<ActionResult> Select() //TODO: [FromBody] QueryObject query
        {
            HttpRequest request = HttpContext.Request;

            if (request.ContentLength == 0)
            {
                return null;
            }

            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                Converters =
                {
                    new QueryObjectJsonConverter(_domain)
                }
            };

            QueryObject query = await JsonSerializer.DeserializeAsync<QueryObject>(request.Body, options);

            List<EntityObject> result = _source.Select(query);

            string json = JsonSerializer.Serialize(result, options);

            return Content(json);
        }
        [HttpPost("create")] public ActionResult Create()
        {
            return null;
        }
        [HttpPut("update")] public ActionResult Update()
        {
            return null;
        }
        [HttpDelete("delete")] public ActionResult Delete()
        {
            return null;
        }
    }
}