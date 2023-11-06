using DaJet.Data;
using DaJet.Model;
using Microsoft.Extensions.DependencyInjection;

namespace DaJet.Flow
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDaJetFlow(this IServiceCollection services, string connectionString)
        {
            AssemblyManager manager = new();
            manager.Register(typeof(Pipeline).Assembly);
            manager.Register("flow");
            manager.Register("protobuf");
            services.AddSingleton<IAssemblyManager>(manager);

            services.AddSingleton<IDomainModel, DomainModel>();
            services.AddSingleton<IDataSource, DaJetDataSource>();
            services.AddSingleton(serviceProvider =>
            {
                return new DataSourceOptions()
                {
                    ConnectionString = connectionString
                };
            });

            services.AddSingleton<GenericOptionsFactory>();
            services.AddSingleton<OptionsFactoryProvider>();
            services.AddSingleton<IPipelineFactory, PipelineFactory>();
            services.AddSingleton<IPipelineManager, PipelineManager>();
            
            services.AddHostedService<DaJetFlowService>();

            return services;
        }
    }
}