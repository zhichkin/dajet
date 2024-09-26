using DaJet.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace DaJet
{
    public static class Program
    {
        private const string DEFAULT_ROOT_PATH = "scripts";
        private const string DAJET_SCRIPT_FILE_EXTENSION = ".djs";
        private static HostConfig Config { get; set; } = new();
        public static void Main(string[] args)
        {
            if (args is not null && args.Length > 0)
            {
                string extension = Path.GetExtension(args[0]);

                if (string.IsNullOrEmpty(extension))
                {
                    Console.WriteLine($"[400][BAD REQUEST] {args[0]}");
                }
                else if (!File.Exists(args[0]))
                {
                    Console.WriteLine($"[404][NOT FOUND] {args[0]}");
                }
                else if (extension == ".json")
                {
                    RunHost(args[0]);
                }
                else if (extension == DAJET_SCRIPT_FILE_EXTENSION)
                {
                    RunScript(args[0]);
                }
                else
                {
                    Console.WriteLine($"[422][UNPROCESSABLE ENTITY] {args[0]}");
                }
            }
            else
            {
                RunHost(null); // default host settings
            }
        }
        private static void RunScript(in string filePath)
        {
            Console.WriteLine("[HOST] Running");
            Console.WriteLine($"[SCRIPT] {filePath}");

            StreamManager.LogToConsole();
            StreamManager.IgnoreErrors(true);

            Stopwatch watch = new();

            watch.Start();

            Dictionary<string, object> parameters = new();

            bool success = true;

            try
            {
                StreamManager.Execute(in filePath, in parameters, out object result);

                if (result is not null)
                {
                    Console.WriteLine(result.ToString()); // RETURN statement
                }
            }
            catch (Exception error)
            {
                success = false;
                Console.WriteLine("[500][INTERNAL SERVER ERROR]");
                Console.WriteLine(ExceptionHelper.GetErrorMessage(error));
            }
            finally
            {
                StreamManager.LogToFile();
                StreamManager.IgnoreErrors(false);
            }

            watch.Stop();

            long elapsed = watch.ElapsedMilliseconds;

            if (success)
            {
                Console.WriteLine($"[200][TIME {elapsed} ms]");
            }

            Console.WriteLine("[HOST] Stopped");
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
            FileLogger.Default.Write($"[LOG SIZE] {Config.LogSize} bytes");
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
                Config.RootPath = Path.Combine(AppContext.BaseDirectory, DEFAULT_ROOT_PATH);
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

            services.AddHostedService<DaJetScriptService>();
        }
    }
}