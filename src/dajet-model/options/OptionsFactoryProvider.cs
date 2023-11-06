using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DaJet.Model
{
    public sealed class OptionsFactoryProvider
    {
        private readonly IServiceProvider _services;
        private readonly IAssemblyManager _assemblies;
        private readonly GenericOptionsFactory _factory;
        private readonly Dictionary<Type, IOptionsFactory> _factories = new();
        public OptionsFactoryProvider(IServiceProvider services, IAssemblyManager assemblies)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _assemblies = assemblies ?? throw new ArgumentNullException(nameof(assemblies));

            _factory = services.GetService<GenericOptionsFactory>();

            Initialize();
        }
        private void Initialize()
        {
            foreach (Assembly assembly in _assemblies.Assemblies)
            {
                Initialize(assembly);
            }
        }
        private void Initialize(Assembly assembly)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            foreach (Type type in assembly.GetTypes())
            {
                RegisterFactory(type);
            }
        }
        private void RegisterFactory(Type factoryType)
        {
            if (factoryType is null)
            {
                throw new ArgumentNullException(nameof(factoryType));
            }

            if (!factoryType.IsOptionsFactory(out Type optionsType))
            {
                return;
            }

            object instance = ActivatorUtilities.CreateInstance(_services, factoryType);

            if (instance is IOptionsFactory factory)
            {
                _ = _factories.TryAdd(optionsType, factory);
            }
        }
        public IOptionsFactory GetFactory(Type optionsType)
        {
            if (!optionsType.IsSubclassOf(typeof(OptionsBase)))
            {
                throw new ArgumentOutOfRangeException(nameof(optionsType));
            }

            if (_factories.TryGetValue(optionsType, out IOptionsFactory factory))
            {
                return factory;
            }
            else
            {
                return _factory;
            }
        }
        public IOptionsFactory<TOptions> GetFactory<TOptions>() where TOptions : OptionsBase, new()
        {
            if (_factories.TryGetValue(typeof(TOptions), out IOptionsFactory value))
            {
                if (value is IOptionsFactory<TOptions> factory)
                {
                    return factory;
                }
            }

            return null;
        }
        public IOptionsFactory GetRequiredFactory(Type optionsType)
        {
            return GetFactory(optionsType) ?? throw new InvalidOperationException($"Options factory for [{optionsType}] not found.");
        }
        public IOptionsFactory<TOptions> GetRequiredFactory<TOptions>() where TOptions : OptionsBase, new()
        {
            return GetFactory<TOptions>() ?? throw new InvalidOperationException($"Options factory for [{typeof(TOptions)}] not found.");
        }
    }
}