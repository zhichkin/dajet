using DaJet.Data;
using DaJet.Model;

namespace DaJet.Flow.Script
{
    public sealed class ScriptOptionsFactory : OptionsFactory<ScriptOptions>
    {
        public ScriptOptionsFactory(IDomainModel domain, IDataSource source) : base(domain, source) { }
        protected override void Configure(Entity owner, in ScriptOptions options,
            in IEnumerable<OptionRecord> values, in IEnumerable<OptionRecord> notset)
        {
            if (notset is not List<OptionRecord> user_provided_options)
            {
                return;
            }
            
            options.Parameters = new Dictionary<string, object>(user_provided_options.Count);

            foreach (OptionRecord option in user_provided_options)
            {
                options.Parameters.Add(option.Name, option.Value);

                //if (option.Type == "string")
                //{
                //    options.Parameters.Add(option.Name, option.Value);
                //}
            }
        }
    }
}