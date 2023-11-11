using DaJet.Data;
using DaJet.Model;

namespace DaJet.Flow
{
    public sealed class PipelineOptionsFactory : OptionsFactory<PipelineOptions>
    {
        public PipelineOptionsFactory(IDomainModel domain, IDataSource source) : base(domain, source) { }
        protected override void Configure(Entity owner, in PipelineOptions options,
            in IEnumerable<OptionRecord> values, in IEnumerable<OptionRecord> notset)
        {
            PipelineRecord record = _source.Select<PipelineRecord>(owner)
                ?? throw new InvalidOperationException($"Pipeline not found: {owner}");

            options.Uuid = owner.Identity;
            options.Name = record.Name;
            options.Activation = record.Activation;
        }
    }
}