using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace DaJet.Http.Controllers
{
    [Route("home")]
    public class HomeController : ODataController
    {
        public HomeController() { }
        
        [EnableQuery]
        [HttpGet("TreeNodeRecord")]
        public ActionResult<IEnumerable<TreeNodeRecord>> Select()
        {
            return new List<TreeNodeRecord>()
            {
                new()
                {
                    Identity = new Guid("b0ff0ee5-dfb7-4f88-8fcb-58e56e190d57"),
                    Name = "Node 1",
                    Value = new PipelineRecord() { Name = "Pipeline 1" }
                },
                new()
                {
                    Identity = new Guid("b0ff0ee5-dfb7-8fcb-4f88-58e56e190d57"),
                    Name = "Node 2",
                    Value = new PipelineRecord() { Name = "Pipeline 2" }
                },
                new()
                {
                    Identity = new Guid("b0ff0ee5-4f88-8fcb-dfb7-58e56e190d57"),
                    Name = "Node 3",
                    Value = new PipelineBlockRecord() { Name = "Pipeline Block 123" }
                }
            };
        }

        [EnableQuery]
        [HttpGet("TreeNodeRecord({uuid:guid})")]
        public ActionResult<TreeNodeRecord> Select([FromRoute] Guid uuid)
        {
            return new TreeNodeRecord()
            {
                Identity = uuid,
                Name = $"Node 1",
                Value = new PipelineRecord() { Name = "Pipeline 1"}
            };
        }
    }
}