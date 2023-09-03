using Microsoft.Extensions.DependencyInjection;

namespace DaJet.Flow
{
    public static class DaJetFlowExtensions
    {
        public static IServiceCollection AddDaJetFlow(this IServiceCollection services, string connectionString)
        {
            PipelineOptionsProvider options = new(connectionString);
            
            services.AddSingleton<IPipelineOptionsProvider>(options);
            services.AddSingleton<IPipelineBuilder, PipelineBuilder>();
            services.AddSingleton<IPipelineManager, PipelineManager>();
            services.AddHostedService<DaJetFlowService>();

            return services;
        }
    }
}