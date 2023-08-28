using DaJet.Model;
using Microsoft.Extensions.DependencyInjection;

namespace DaJet.Dto.Server
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseDataSource(this IServiceCollection services, string connectionString)
        {
            DataSourceOptions options = new()
            {
                ConnectionString = connectionString
            };

            services.AddSingleton(options);
            services.AddSingleton<IDataSource, DataSource>();

            return services;
        }
    }
}
