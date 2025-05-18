using DaJet.Http.Client;
using DaJet.Model;
using DaJet.Studio.Controllers;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

namespace DaJet.Studio
{
    public static class Program
    {
        private const string DAJET_HTTP_CLIENT = "DaJetClient";
        private const string DAJET_HOST_SETTING = "DaJetHost";
        public static async Task Main(string[] args)
        {
            WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddHttpClient(DAJET_HTTP_CLIENT, (services, client) =>
            {
                IConfiguration configuration = services.GetRequiredService<IConfiguration>();
                IWebAssemblyHostEnvironment environment = services.GetRequiredService<IWebAssemblyHostEnvironment>();

                string DaJetHost = configuration[DAJET_HOST_SETTING];

                if (string.IsNullOrWhiteSpace(DaJetHost))
                {
                    client.BaseAddress = new Uri(environment.BaseAddress);
                }
                else
                {
                    client.BaseAddress = new Uri(DaJetHost);
                }
            });
            builder.Services.AddSingleton(services =>
            {
                return services.GetRequiredService<IHttpClientFactory>().CreateClient(DAJET_HTTP_CLIENT);
            });

            builder.Services.AddSingleton<IDomainModel, DomainModel>();
            builder.Services.AddSingleton<DaJetHttpClient>();

            builder.Services.AddMudServices();
            builder.Services.AddSingleton<AppState>();
            builder.Services.AddSingleton<MonacoEditor>();
            builder.Services.AddSingleton<DaJetCodeController>();
            builder.Services.AddSingleton<MdTreeViewController>();
            builder.Services.AddScoped<FlowTreeViewController>();
            builder.Services.AddScoped<DbViewController>();
            builder.Services.AddScoped<ApiTreeViewController>();
            builder.Services.AddScoped<ExchangeTreeViewController>();
            
            await builder.Build().RunAsync();
        }
    }
}