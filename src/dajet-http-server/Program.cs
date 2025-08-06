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
        private static readonly string[] webapi =
        [
            "/dajet", "/md", "/mdex", "/api", "/query", "/data", "/flow", "/db", "/exchange"
        ];
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
            ShowDaJetVersion();

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
        private static void ShowDaJetVersion()
        {
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            if (version is not null)
            {
                Console.WriteLine($"DaJet {version.Major}.{version.Minor}.{version.Build}");
            }
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
        private static void ConfigureMetadataService(IServiceCollection services)
        {
            services.AddSingleton<IMetadataService>(services =>
            {
                MetadataService metadataService = MetadataService.Cache;

                IDataSource source = services.GetRequiredService<IDataSource>();

                IEnumerable<InfoBaseRecord> list = source.Query<InfoBaseRecord>();

                foreach (InfoBaseRecord record in list)
                {
                    if (!Enum.TryParse(record.DatabaseProvider, out DatabaseProvider provider))
                    {
                        provider = DatabaseProvider.SqlServer;
                    }

                    string cacheKey;
                    try
                    {
                        cacheKey = DbConnectionFactory.GetCacheKey(provider, record.ConnectionString, record.UseExtensions);
                    }
                    catch (Exception exception)
                    {
                        string description = "Ошибка добавления базы данных: неверный формат строки подключения!" + Environment.NewLine;
                        description += ExceptionHelper.GetErrorMessage(exception);
                        FileLogger.Default.Write(description);
                        continue;
                    }

                    metadataService.Add(new InfoBaseOptions()
                    {
                        CacheKey = cacheKey,
                        UseExtensions = record.UseExtensions,
                        DatabaseProvider = provider,
                        ConnectionString = record.ConnectionString
                    });
                }

                return metadataService;
            });
        }
    }
}