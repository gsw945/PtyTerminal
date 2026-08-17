using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PtyWeb.SignalR
{
    /// <summary>
    /// ASP.NET Core + SignalR demo: hosts the terminal frontend (xterm.js) and relays
    /// pty output/input over a SignalR hub at /terminalHub.
    ///   - client -> server: Input(string), Resize(cols, rows)
    ///   - server -> client: Output(byte[]), Closed(exitCode)
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args)
        {
            // Serve wwwroot from the source tree when running in the project directory
            // (dotnet run, editable without rebuild); otherwise fall back to the copy
            // next to the executable (direct exe launch).
            string contentRoot = Directory.GetCurrentDirectory();
            if (!Directory.Exists(Path.Combine(contentRoot, "wwwroot")) &&
                Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot")))
            {
                contentRoot = AppContext.BaseDirectory;
            }

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = contentRoot,
            });

            // Default port 8879; override with the first command line argument or ASPNETCORE_URLS.
            string url = args.Length > 0 ? args[0] : "http://*:8879";
            builder.WebHost.UseUrls(url);

            builder.Services.AddSignalR();
            builder.Services.AddSingleton<TerminalSessionManager>();

            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapHub<TerminalHub>("/terminalHub");

            Console.WriteLine($"PtyWeb.SignalR listening on {url.Replace("*", "localhost", StringComparison.Ordinal)} (hub at /terminalHub)");

            app.Run();
        }
    }
}
