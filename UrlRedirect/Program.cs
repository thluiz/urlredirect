using EmbedIO;
using EmbedIO.Actions;
using Microsoft.Extensions.Hosting;
using Swan.Logging;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace UrlRedirect {
    class Program {
        static Dictionary<string, string> Redirects = new Dictionary<string, string>();

        static void Main(string[] args) {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args) {
            var baseUrl = "myvtmi.im";

            if (args.Length > 0)
                baseUrl = args[0];

            UpdateRedirects();

            var hostBuilder = new HostBuilder()
                .ConfigureServices((hostContext, services) => {                    

                    using (var server = CreateWebServer(baseUrl)) {
                        
                        server.RunAsync();

                    }

                });

            await hostBuilder.RunConsoleAsync();
        }

        private static void UpdateRedirects() {
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
                SqlDataReader reader = cmd.ExecuteReader();
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

        // Create and configure our web server.
        private static WebServer CreateWebServer(string baseUrl) {
            var server = new WebServer(o => o
                .WithUrlPrefix($"http://*:{Environment.GetEnvironmentVariable("PORT")}")
                .WithMode(HttpListenerMode.EmbedIO))
                .WithModule(new ActionModule("/update", HttpVerbs.Any,
                    ctx => {
                        UpdateRedirects();
                        return ctx.SendStringAsync("Updated!", "text", Encoding.ASCII);
                    })
                ).WithModule(new ActionModule("/", HttpVerbs.Any,
                    ctx => {
                        var requestHost = ctx.Request.Url.Host;
                        var idx = requestHost.IndexOf($".{baseUrl}");

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

        private static string WriteHtmlRedirect(string redirectURL) {
            return $"<html><head><meta http-equiv=\"refresh\" content=\"0; URL = {redirectURL}\" /></head><body><script>location.href = \"{redirectURL}\";</script></body></html>";
        }
    }
}
