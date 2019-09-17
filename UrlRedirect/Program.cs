using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace UrlRedirect {
    class Program {

        static async Task Main(string[] args) {

            var hostBuilder = new HostBuilder()
                .ConfigureServices((hostContext, services) => {
                    services.AddSingleton<IHostedService, RedirectService>();
                });

            await hostBuilder.RunConsoleAsync();
        }
    }
}
