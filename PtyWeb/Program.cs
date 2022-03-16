using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.Files;
using EmbedIO.WebApi;
using Swan.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PtyWeb
{
    class Program
    {
        static void Main(string[] args)
        {
            // CliDemo.Run();

            var url = args.Length > 0 ? args[0] : "http://*:8877";

            using (var cts = new CancellationTokenSource())
            {
                Task.WaitAll(
                    RunWebServerAsync(url, cts.Token),
                    OpenBrowser ? ShowBrowserAsync(url.Replace("*", "localhost", StringComparison.Ordinal), cts.Token) : Task.CompletedTask,
                    OnCtrlC2Exit(cts.Cancel)
                );
            }

            // Clean up
            "Bye".Info(nameof(Program));
            Swan.Terminal.Flush();
        }

        private const bool OpenBrowser = false;
        private const bool UseFileCache = false;

        // Gets the local path of shared files.
        // When debugging, take them directly from source so we can edit and reload.
        // Otherwise, take them from the deployment directory.
        public static string HtmlRootPath
        {
            get
            {
                var assemblyLocation = typeof(Program).Assembly.Location;
                if (string.IsNullOrEmpty(assemblyLocation))
                {
                    assemblyLocation = Process.GetCurrentProcess().MainModule.FileName;
                }
                var assemblyPath = Path.GetDirectoryName(assemblyLocation);

#if DEBUG
                return Path.Combine(Directory.GetParent(assemblyPath).Parent.Parent.FullName, "html");
#else
                return Path.Combine(assemblyPath, "html");
#endif
            }
        }

        // Create and configure our web server.
        private static WebServer CreateWebServer(string url)
        {
            var server = new WebServer(o => o.WithUrlPrefix(url).WithMode(HttpListenerMode.EmbedIO))
                // First, we will configure our web server by adding Modules.
                .WithLocalSessionManager()
                .WithModule(new ActionModule("/error", HttpVerbs.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })))
                .WithWebApi("/api", m => m.WithController<DemoController>())
                .WithModule(new WebSocketPtyModule("/terminal"))
                .WithStaticFolder("/", HtmlRootPath, true, m => m.WithContentCaching(UseFileCache)) // Add static files after other modules to avoid conflicts
                ;

            // Listen for state changes.
            server.StateChanged += (s, e) => $"WebServer New State: {e.NewState}".Info();

            return server;
        }

        // Create and run a web server.
        private static async Task RunWebServerAsync(string url, CancellationToken cancellationToken)
        {
            using var server = CreateWebServer(url);
            await server.RunAsync(cancellationToken).ConfigureAwait(false);
        }

        // Open the default browser on the web server's home page.
        private static async Task ShowBrowserAsync(string url, CancellationToken cancellationToken)
        {
            // Be sure to run in parallel.
            await Task.Yield();

            // Fire up the browser to show the content!
            using var browser = new Process()
            {
                StartInfo = new ProcessStartInfo(url)
                {
                    UseShellExecute = true,
                },
            };
            browser.Start();
        }

        // wait for a Ctrl + C press
        // call the specified action to cancel operations.
        private static async Task OnCtrlC2Exit(Action cancel)
        {
            "Press [Ctrl + C] to stop the web server.".Info(nameof(Program));
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                "Stopping...".Info(nameof(Program));
                cancel();
            };
            await Task.CompletedTask;
        }
    }
}
