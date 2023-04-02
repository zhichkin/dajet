using DaJet.Data;
using DaJet.Flow;
using DaJet.Http.DataMappers;
using DaJet.Http.Model;
using DaJet.Metadata;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;

namespace DaJet.Http.Server
{
    public static class Program
    {
        public static string DATABASE_FILE_NAME = "dajet.db";
        private static readonly string[] webapi = new string[] { "/api", "/md", "/mdex", "/db", "/query", "/flow" };
        public static void Main(string[] args)
        {
            WebApplicationOptions options = new()
            {
                Args = args,
                WebRootPath = "ui",
                ContentRootPath = AppContext.BaseDirectory
            };

            WebApplicationBuilder builder = WebApplication.CreateBuilder(options);
            builder.Host.UseSystemd();
            builder.Host.UseWindowsService();
            builder.Services.AddControllers();
            builder.Services.AddCors(ConfigureCors);
            ConfigureFlowService(builder.Services);
            ConfigureMetadataService(builder.Services);
            ConfigureFileProvider(builder.Services);

            WebApplication app = builder.Build();
            //app.UseAuthentication();
            //app.UseAuthorization();
            app.UseCors();
            app.UseHttpsRedirection();
            app.MapWhen(IsWebApiRequest, ConfigureWebApiPipeline);
            app.MapWhen(IsBlazorRequest, ConfigureBlazorPipeline);
            app.Run();
        }
        private static void ConfigureCors(CorsOptions options)
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        }
        private static bool IsWebApiRequest(HttpContext context)
        {
            for (int i = 0; i < webapi.Length; i++)
            {
                if (context.Request.Path.StartsWithSegments(webapi[i]))
                {
                    return true;
                }
            }
            return false;
        }
        private static bool IsBlazorRequest(HttpContext context)
        {
            return !IsWebApiRequest(context);
        }
        private static void ConfigureWebApiPipeline(IApplicationBuilder builder)
        {
            builder.UseRouting().UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
        private static void ConfigureBlazorPipeline(IApplicationBuilder builder)
        {
            builder.UseDefaultFiles();
            builder.UseStaticFiles(new StaticFileOptions()
            {
                ServeUnknownFileTypes = true // .wasm + .dll + .blat + .dat
            });
            builder.UseRouting().UseEndpoints(endpoints =>
            {
                endpoints.MapFallbackToFile("/index.html");
            });
        }
        private static void ConfigureFlowService(IServiceCollection services)
        {
            string databaseFileFullPath = Path.Combine(AppContext.BaseDirectory, DATABASE_FILE_NAME);

            string connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = databaseFileFullPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }
            .ToString();

            PipelineManager manager = new(connectionString);
            services.AddSingleton<IPipelineManager>(manager);
            services.AddSingleton<IPipelineBuilder, PipelineBuilder>();
            services.AddHostedService<DaJetFlowService>();
        }
        private static void ConfigureMetadataService(IServiceCollection services)
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
                    Key = entity.Uuid.ToString(),
                    UseExtensions = entity.UseExtensions,
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