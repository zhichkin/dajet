using DaJet.Data;
using DaJet.Http.DataMappers;
using DaJet.Http.Model;
using DaJet.Metadata;
using Microsoft.Extensions.FileProviders;

namespace DaJet.Http.Server
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationOptions options = new()
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            };

            WebApplicationBuilder builder = WebApplication.CreateBuilder(options);

            builder.Host.UseSystemd();
            builder.Host.UseWindowsService();

            // Allow CORS (Cross Origin Resource Sharing)
            builder.Services.AddCors();
            // Add services to the container.
            builder.Services.AddControllers();

            ConfigureServices(builder.Services);
            ConfigureFileProvider(builder.Services);

            WebApplication app = builder.Build();
            app.UseCors(policy => policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());
            
            //app.UseHttpsRedirection();
            
            app.MapControllers();

            //app.UseAuthentication();
            //app.UseAuthorization();

            app.Run();
        }
        private static void ConfigureServices(IServiceCollection services)
        {
            MetadataService metadataService = new();

            InfoBaseDataMapper mapper = new();
            List<InfoBaseModel> list = mapper.Select();
            foreach (InfoBaseModel entity in list)
            {
                if (!Enum.TryParse(entity.DatabaseProvider, out DatabaseProvider provider))
                {
                    provider = DatabaseProvider.SqlServer;
                }

                metadataService.Add(new InfoBaseOptions()
                {
                    Key = entity.Name,
                    UseExtensions = true,
                    DatabaseProvider = provider,
                    ConnectionString = entity.ConnectionString
                });
            }

            services.AddSingleton<IMetadataService>(metadataService);
        }
        private static void ConfigureFileProvider(IServiceCollection services)
        {
            string catalogPath = AppContext.BaseDirectory;
            
            PhysicalFileProvider fileProvider = new(catalogPath);

            services.AddSingleton<IFileProvider>(fileProvider);
        }
    }
}