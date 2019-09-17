using EmbedIO;
using EmbedIO.Actions;
using Microsoft.Extensions.Hosting;
using Swan.Logging;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UrlRedirect {
    class RedirectService : IHostedService, IDisposable {
        private static Dictionary<string, string> Redirects = new Dictionary<string, string>();

        private WebServer ServerInstance { get; set; }

        public Task StartAsync(CancellationToken cancellationToken) {

            UpdateRedirects();

            ServerInstance = CreateWebServer();

            return ServerInstance.RunAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            Dispose();

            return Task.CompletedTask;
        }

        private void UpdateRedirects() {
            Console.WriteLine("Loading URLs\n");

            var updates = 0;
            var inserts = 0;

            string connectionString =
                $"Data Source={Environment.GetEnvironmentVariable("DB_PATH")};" +
                $"Initial Catalog={Environment.GetEnvironmentVariable("DB_NAME")};" +
                $"User ID={Environment.GetEnvironmentVariable("DB_USER")};" +
                $"Password={Environment.GetEnvironmentVariable("DB_PASS")};";

            using (var connection = new SqlConnection(connectionString)) {
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT [url], [target] FROM URL where URL_TYPE = 1";

                connection.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var url = reader["url"].ToString().Trim();
                    var target = reader["target"].ToString().Trim();

                    if (!Redirects.ContainsKey(url)) {
                        Redirects.Add(url, target);
                        inserts++;
                    } else if (Redirects[url] != target) {
                        Redirects[url] = target;
                        updates++;
                    }
                }

                connection.Close();
            }

            if (inserts > 0)
                Console.WriteLine($"Loaded {inserts} URLs\n");

            if (updates > 0)
                Console.WriteLine($"Updated {updates} URLs\n");

            if (inserts == 0 && updates == 0)
                Console.WriteLine($"Nothing changed\n");
        }

        private WebServer CreateWebServer() {
            var baseUrl = Environment.GetEnvironmentVariable("BASE_URL");

            var server = new WebServer(o => o
                .WithUrlPrefix($"http://*:{Environment.GetEnvironmentVariable("PORT")}")
                .WithMode(HttpListenerMode.EmbedIO))
                .WithModule(new ActionModule("/healthcheck", HttpVerbs.Any,
                    ctx => {                        
                        return ctx.SendStringAsync("Ok", "text", Encoding.ASCII);
                    })
                )
                .WithModule(new ActionModule("/update", HttpVerbs.Any,
                    ctx => {
                        UpdateRedirects();
                        return ctx.SendStringAsync("Updated!", "text", Encoding.ASCII);
                    })
                ).WithModule(new ActionModule("/", HttpVerbs.Any,
                    ctx => {
                        var requestHost = ctx.Request.Url.Host;
                        var idx = requestHost.LastIndexOf(".");

                        var subdomain = idx > 0 ?
                                        requestHost.Substring(0, idx)
                                        : "*";

                        var redirectURL = Redirects.ContainsKey(subdomain) ?
                                            Redirects[subdomain]
                                            : Redirects["*"];

                        ctx.Response.Headers.Add("Location", redirectURL);

                        return ctx.SendStandardHtmlAsync(302, writer => {
                            writer.Write(WriteHtmlRedirect(redirectURL));
                        });
                    })
                );

            server.StateChanged += (s, e) => $"WebServer New State - {e.NewState}".Info();

            return server;
        }

        private string WriteHtmlRedirect(string redirectURL) {
            return $"<html><head><meta http-equiv=\"refresh\" content=\"0; URL = {redirectURL}\" /></head><body><script>location.href = \"{redirectURL}\";</script></body></html>";
        }

        public void Dispose() {
            if (ServerInstance != null)
                ServerInstance.Dispose();

            if (Redirects != null)
                Redirects = null;
        }
    }
}
