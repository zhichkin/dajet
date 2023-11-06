using Confluent.Kafka;
using DaJet.Data;
using DaJet.Model;

namespace DaJet.Flow.Kafka
{
    public sealed class ConsumerOptionsFactory : OptionsFactory<ConsumerOptions>
    {
        public ConsumerOptionsFactory(IDomainModel domain, IDataSource source) : base(domain, source) { }
        protected override void Configure(in ConsumerOptions options, in IEnumerable<OptionRecord> values, in IEnumerable<OptionRecord> notset)
        {
            options.Config = new ConsumerConfig();

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