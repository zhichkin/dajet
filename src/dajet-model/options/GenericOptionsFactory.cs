using DaJet.Data;
using System.Collections.Generic;
using System;

namespace DaJet.Model
{
    public sealed class GenericOptionsFactory : IOptionsFactory
    {
        private readonly IDataSource _source;
        private readonly IDomainModel _domain;
        public GenericOptionsFactory(IDomainModel domain, IDataSource source)
        {
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }
        public OptionsBase Create(Type type, Entity owner)
        {
            if (!type.IsSubclassOf(typeof(OptionsBase)))
            {
                throw new InvalidOperationException();
            }

            if (_source.Select(owner) is null)
            {
                throw new InvalidOperationException();
            }

            object instance = type.CreateNewInstance();

            if (instance is not OptionsBase options)
            {
                throw new InvalidOperationException();
            }

            options.Owner = owner;

            int typeCode = _domain.GetTypeCode(typeof(OptionRecord));

            if (_source.Select(typeCode, owner) is not List<OptionRecord> values)
            {
                return options; //NOTE: default option values
            }

            foreach (OptionRecord option in values)
            {
                _ = options.Set(option.Name, option.Value);
            }

            return options;
        }
    }
}