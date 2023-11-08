using DaJet.Data;
using DaJet.Flow;
using DaJet.Metadata;
using DaJet.Model;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;
using Microsoft.IO;

namespace DaJet.Http.Server
{
    public static class Program
    {
        private static readonly string DATABASE_FILE_NAME = "dajet.db";
        private static readonly string[] webapi = new string[]
        {
            "/md", "/mdex", "/api", "/query", "/data", "/flow", "/db", "/exchange"
        };
        private static string OptionsFileConnectionString
        {
            get
            {
                string databaseFileFullPath = Path.Combine(AppContext.BaseDirectory, DATABASE_FILE_NAME);

                return new SqliteConnectionStringBuilder()
                {
                    DataSource = databaseFileFullPath,
                    Mode = SqliteOpenMode.ReadWriteCreate
                }
                .ToString();
            }
        }
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
            ConfigureFileProvider(builder.Services);

            ConfigureOptionProviders(builder.Services);
            ConfigureMetadataService(builder.Services);

            builder.Services.AddDaJetFlow(OptionsFileConnectionString);
            builder.Services.AddSingleton<RecyclableMemoryStreamManager>();

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
        private static void ConfigureFileProvider(IServiceCollection services)
        {
            string catalogPath = AppContext.BaseDirectory;

            PhysicalFileProvider fileProvider = new(catalogPath);

            services.AddSingleton<IFileProvider>(fileProvider);
        }
        private static void ConfigureOptionProviders(IServiceCollection services)
        {
            string connectionString = OptionsFileConnectionString;

            services.AddSingleton(new ScriptDataMapper(connectionString));
            services.AddSingleton(new InfoBaseDataMapper(connectionString));
        }
        private static void ConfigureMetadataService(IServiceCollection services)
        {
            MetadataService metadataService = new();

            InfoBaseDataMapper mapper = new(OptionsFileConnectionString);
            
            List<InfoBaseRecord> list = mapper.Select();

            foreach (InfoBaseRecord entity in list)
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
    }
}