using DaJet.Stream;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DaJet
{
    public static class Program
    {
        private static HostConfig Config { get; set; } = new();
        public static void Main(string[] args)
        {
            if (args is not null && args.Length > 0)
            {
                RunHost(args[0]);
            }
            else
            {
                RunHost(null);
            }
        }
        private static void RunHost(in string configFilePath)
        {
            InitializeHostConfig(in configFilePath);

            FileLogger.Default.UseLogFile(Config.LogFile);
            FileLogger.Default.UseLogSize(Config.LogSize);
            FileLogger.Default.UseCatalog(Config.LogPath);

            FileLogger.Default.Write("[HOST] Running");
            FileLogger.Default.Write($"[PATH] {AppContext.BaseDirectory}");
            FileLogger.Default.Write($"[CONFIG] {configFilePath}");
            FileLogger.Default.Write($"[LOG PATH] {Config.LogPath}");
            FileLogger.Default.Write($"[LOG FILE] {Config.LogFile}");
            FileLogger.Default.Write($"[LOG SIZE] {Config.LogSize} Kb");
            FileLogger.Default.Write($"[ROOT] {Config.RootPath}");
            FileLogger.Default.Write($"[REFRESH] {Config.Refresh} seconds");

            CreateHostBuilder().Build().Run();
            
            FileLogger.Default.Write("[HOST] Stopped");
        }
        private static void InitializeHostConfig(in string configFilePath)
        {
            if (File.Exists(configFilePath))
            {
                string path = Path.GetFullPath(configFilePath);

                IConfigurationRoot config = new ConfigurationBuilder()
                    .SetBasePath(Path.GetDirectoryName(path))
                    .AddJsonFile(Path.GetFileName(path), optional: false)
                    .Build();

                config.Bind(Config);
            }
            else if (!string.IsNullOrWhiteSpace(configFilePath))
            {
                FileLogger.Default.Write($"[CONFIG] NOT FOUND");
            }

            if (string.IsNullOrWhiteSpace(Config.LogPath))
            {
                Config.LogPath = AppContext.BaseDirectory;
            }

            if (string.IsNullOrWhiteSpace(Config.RootPath))
            {
                Config.RootPath = Path.Combine(AppContext.BaseDirectory, "stream");
            }
        }
        private static IHostBuilder CreateHostBuilder()
        {
            IHostBuilder builder = Host.CreateDefaultBuilder()
                .UseSystemd()
                .UseWindowsService()
                .ConfigureServices(ConfigureServices);

            return builder;
        }
        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions().AddSingleton(Options.Create(Config));

            services.AddHostedService<DaJetStreamService>();
        }
    }
}