using DaJet.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace DaJet.Http.Controllers
{
    public class TreeNodeRecordController : ODataController
    {
        private static List<TreeNodeRecord> _nodes = new()
        {
            new()
            {
                Parent = new() { Name = "root" },
                Identity = new Guid("b0ff0ee5-dfb7-4f88-8fcb-58e56e190d57"),
                Name = "Node 1",
                Value = new PipelineRecord() { Name = "Pipeline" }
            },
            new()
            {
                Parent = new() { Name = "root" },
                Identity = new Guid("b0ff0ee5-dfb7-8fcb-4f88-58e56e190d57"),
                Name = "Node 2",
                Value = new PipelineBlockRecord() { Name = "Block" }
            },
            new()
            {
                Parent = new() { Name = "Parent 1" },
                Name = "Node 3",
                Identity = new Guid("b0ff0ee5-4f88-8fcb-dfb7-58e56e190d57"),
                Value = new PipelineBlockRecord() { Name = "Pipeline Block 123" }
            }
        };

        [EnableQuery()]
        public ActionResult<TreeNodeRecord> Get([FromRoute] Guid key)
        {
            return new TreeNodeRecord()
            {
                Identity = key,
                Name = $"Node 1",
                Value = new PipelineRecord() { Name = "Pipeline 1" }
            };
        }
        public ActionResult<TreeNodeRecord> GetParent([FromRoute] Guid key)
        {
            TreeNodeRecord value = new() { Identity = key, Name = "Get parent test" };

            return value;
        }
        public ActionResult<EntityObject> GetValue([FromRoute] Guid key)
        {
            TreeNodeRecord node = _nodes.Where(e => e.Identity == key).FirstOrDefault();

            return node?.Value;
        }
        [EnableQuery()]
        public ActionResult<IEnumerable<TreeNodeRecord>> Get()
        {
            return _nodes;
        }
    }
}