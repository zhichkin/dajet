using DaJet.Model;
using Microsoft.OData.Client;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace DaJet.Studio
{
    public partial class ODataSource : DataServiceContext
    {
        public ODataSource(Uri serviceRoot) : base(serviceRoot)
        {
            HttpRequestTransportMode = HttpRequestTransportMode.HttpClient;
            Format.LoadServiceModel = () => GetEdmModel();
            Format.UseJson();

            TreeNodes = base.CreateQuery<TreeNodeRecord>(nameof(TreeNodeRecord));
        }
        public DataServiceQuery<TreeNodeRecord> TreeNodes { get; }
        private IEdmModel GetEdmModel()
        {
            ODataConventionModelBuilder builder = new();

            builder.EntityType<EntityObject>();
            builder.EntityType<TreeNodeRecord>();
            builder.EntityType<PipelineRecord>();
            builder.EntityType<PipelineBlockRecord>();

            builder.EntitySet<EntityObject>("EntityObject")
                .EntityType.HasKey(p => p.Identity).Count().Select().Page(null, 100).Expand().Filter();

            builder.EntitySet<TreeNodeRecord>("TreeNodeRecord")
                .EntityType.HasKey(p => p.Identity).Count().Select().Page(null, 100).Expand().Filter();

            IEdmModel model = builder.GetEdmModel();

            return model;
        }
    }
}