using EmbedIO;
using EmbedIO.Actions;
using Swan.Logging;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace UrlRedirect {
    class Program {
        static Dictionary<string, string> redirects = new Dictionary<string, string>();

        static void Main(string[] args) {
            var baseUrl = "myvtmi.im";

            if (args.Length > 0)
                baseUrl = args[0];

            UpdateRedirects();
            
            // Our web server is disposable.
            using (var server = CreateWebServer(baseUrl)) {
                // Once we've registered our modules and configured them, we call the RunAsync() method.
                server.RunAsync();

                // Wait for any key to be pressed before disposing of our web server.
                // In a service, we'd manage the lifecycle of our web server using
                // something like a BackgroundWorker or a ManualResetEvent.
                Console.ReadKey(true);
            }
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
                cmd.CommandText ="SELECT [url], [target] FROM URL where URL_TYPE = 1";

                connection.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) {
                    var url = reader["url"].ToString();
                    var target = reader["target"].ToString();

                    if (redirects.ContainsKey(url)) {
                        redirects.Add(url, target);
                        inserts++;
                    } else {
                        redirects[url] = target;
                        updates++;
                    }
                }

                connection.Close();
            }

            if(inserts > 0)
                Console.WriteLine($"Loaded {inserts} URLs\n");

            if (updates > 0)
                Console.WriteLine($"Updated {updates} URLs\n");

            if(inserts == 0 && updates == 0)
                Console.WriteLine($"Nothing changed\n");
        }

        // Create and configure our web server.
        private static WebServer CreateWebServer(string baseUrl) {
            var server = new WebServer(o => o
                .WithUrlPrefix($"http://*:{Environment.GetEnvironmentVariable("PORT")}")
                .WithMode(HttpListenerMode.EmbedIO))
                .WithModule(new ActionModule("/update", HttpVerbs.Any, 
                    ctx => {
                        return ctx.SendStringAsync("Updated!", "text", Encoding.ASCII);
                    })
                ).WithModule(new ActionModule("/", HttpVerbs.Any,
                    ctx => {
                    var requestHost = ctx.Request.Url.Host;
                    var idx = requestHost.IndexOf($".{baseUrl}");

                        var subdomain = idx > 0 ?
                                        requestHost.Substring(0, idx)
                                        : "*";

                        var redirectURL = redirects.ContainsKey(subdomain) ?
                                            redirects[subdomain]
                                            : redirects["*"];

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
