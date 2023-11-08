using DaJet.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaJet.Model
{
    public interface IOptionsFactory
    {
        OptionsBase Create(Type type, Entity owner);
    }
    public interface IOptionsFactory<TOptions> : IOptionsFactory where TOptions : OptionsBase, new()
    {
        TOptions Create(Entity owner);
    }
    public abstract class OptionsFactory<TOptions> : IOptionsFactory<TOptions> where TOptions : OptionsBase, new()
    {
        protected readonly IDataSource _source;
        protected readonly IDomainModel _domain;
        public OptionsFactory(IDomainModel domain, IDataSource source)
        {
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }
        OptionsBase IOptionsFactory.Create(Type type, Entity owner)
        {
            if (type == typeof(TOptions))
            {
                return Create(owner);
            }
            return null;
        }
        public TOptions Create(Entity owner)
        {
            TOptions options = new()
            {
                Owner = owner
            };

            IEnumerable<OptionRecord> values = _source.Query<OptionRecord>(owner);

            if (values is null || !values.Any())
            {
                Configure(in options);
            }
            else
            {
                Configure(in options, in values);
            }
            
            return options;
        }
        protected virtual void Configure(in TOptions options)
        {
            // do nothing by default
        }
        protected virtual void Configure(in TOptions options, in IEnumerable<OptionRecord> values)
        {
            if (values is not null)
            {
                List<OptionRecord> notset = null;

                foreach (OptionRecord option in values)
                {
                    if (!options.Set(option.Name, option.Value))
                    {
                        notset ??= new List<OptionRecord>();

                        notset.Add(option);
                    }
                }

                Configure(in options, in values, notset);
            }
        }
        protected virtual void Configure(in TOptions options, in IEnumerable<OptionRecord> values, in IEnumerable<OptionRecord> notset)
        {
            // do nothing by default
        }
    }
}