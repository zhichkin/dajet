using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace DaJet.Studio
{
    public static class Program
    {
        private const string DAJET_HTTP_CLIENT = "DaJetClient";
        private const string DAJET_HOST_SETTING = "DaJetHost";
        public async static Task Main(string[] args)
        {
            WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

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

            await builder.Build().RunAsync();
        }
    }
}