using DaJet.Model;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DaJet
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseDomainFromAssembly(this IServiceCollection services, Assembly assembly)
        {
            services.AddSingleton(serviceProvider =>
            {
                return new DomainModel(serviceProvider).ConfigureFromAssembly(in assembly, in services);
            });

            return services;
        }
    }
}