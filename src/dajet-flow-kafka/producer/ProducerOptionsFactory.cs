using Confluent.Kafka;
using DaJet.Data;
using DaJet.Model;

namespace DaJet.Flow.Kafka
{
    public sealed class ProducerOptionsFactory : OptionsFactory<ProducerOptions>
    {
        public ProducerOptionsFactory(IDomainModel domain, IDataSource source) : base(domain, source) { }
        protected override void Configure(in ProducerOptions options, in IEnumerable<OptionRecord> values, in IEnumerable<OptionRecord> notset)
        {
            options.Config = new ProducerConfig();

            if (notset is not null)
            {
                foreach (OptionRecord option in notset)
                {
                    string key = ConfigHelper.GetOptionKey(option.Name);

                    options.Config.Set(key, option.Value);
                }
            }
        }
    }
}