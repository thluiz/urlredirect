using EmbedIO;
using EmbedIO.Actions;
using Swan.Logging;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace UrlRedirect {
    class Program {
        static void Main(string[] args) {
            var baseUrl = "myvtmi.im";

            if (args.Length > 0)
                baseUrl = args[0];

            var redirects = GetRedirects();

            // Our web server is disposable.
            using (var server = CreateWebServer(baseUrl, redirects)) {
                // Once we've registered our modules and configured them, we call the RunAsync() method.
                server.RunAsync();

                var browser = new System.Diagnostics.Process() {
                    StartInfo = new System.Diagnostics.ProcessStartInfo($"http://{baseUrl}") { UseShellExecute = true }
                };
                browser.Start();
                // Wait for any key to be pressed before disposing of our web server.
                // In a service, we'd manage the lifecycle of our web server using
                // something like a BackgroundWorker or a ManualResetEvent.
                Console.ReadKey(true);
            }
        }

        private static Dictionary<string, string> GetRedirects() {
            var resp = new Dictionary<string, string>();

            Console.WriteLine("Loading URLs\n");

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
                    //Console.WriteLine($"{reader["CodigoTelefonico"]} {reader["NomePais"]} ({reader["Sigla"]})");

                    resp.Add(reader["url"].ToString(), reader["target"].ToString());
                }

                connection.Close();
            }

            Console.WriteLine($"Loaded {resp.Count} URLs\n");

            return resp;
        }

        // Create and configure our web server.
        private static WebServer CreateWebServer(string baseUrl, Dictionary<string, string> redirects) {
            var server = new WebServer(o => o
                .WithUrlPrefix($"http://*:{Environment.GetEnvironmentVariable("PORT")}")
                .WithMode(HttpListenerMode.EmbedIO))                
                .WithModule(new ActionModule("/", HttpVerbs.Any,
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
