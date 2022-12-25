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
                WebRootPath = "ui",
                ContentRootPath = AppContext.BaseDirectory
            };

            WebApplicationBuilder builder = WebApplication.CreateBuilder(options);
            builder.Host.UseSystemd();
            builder.Host.UseWindowsService();
            builder.Services.AddControllers();
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
                });
            });
            ConfigureServices(builder.Services);
            ConfigureFileProvider(builder.Services);

            WebApplication app = builder.Build();
            //app.UseAuthentication();
            //app.UseAuthorization();
            app.UseCors();
            app.UseHttpsRedirection();

            //(!?) context.Request.Host.Port == 5001

            app.MapWhen(context =>
            {
                return context.Request.Path.StartsWithSegments("/md")
                || context.Request.Path.StartsWithSegments("/mdex")
                || context.Request.Path.StartsWithSegments("/1ql");
            },
            builder =>
            {
                builder.UseRouting().UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            });

            app.MapWhen((context) =>
            {
                return !(context.Request.Path.StartsWithSegments("/md")
                || context.Request.Path.StartsWithSegments("/mdex")
                || context.Request.Path.StartsWithSegments("/1ql"));
            },
            builder =>
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
            });

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