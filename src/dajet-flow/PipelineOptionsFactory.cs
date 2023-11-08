using DaJet.Data;
using DaJet.Model;

namespace DaJet.Flow
{
    public sealed class PipelineOptionsFactory : OptionsFactory<PipelineOptions>
    {
        public PipelineOptionsFactory(IDomainModel domain, IDataSource source) : base(domain, source) { }
        protected override void Configure(in PipelineOptions options, in IEnumerable<OptionRecord> values, in IEnumerable<OptionRecord> notset)
        {
            PipelineRecord record = _source.Select<PipelineRecord>(options.Owner)
                ?? throw new InvalidOperationException($"Pipeline not found: {options.Owner}");

            options.Name = record.Name;
            options.Activation = record.Activation;
        }
    }
}