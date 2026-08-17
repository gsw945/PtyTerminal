using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace PtyWeb.AspNetCore
{
    /// <summary>
    /// ASP.NET Core WebSocket demo: hosts the same terminal frontend (xterm.js +
    /// attach addon) and relays pty output/input over a raw WebSocket at /terminal.
    /// Wire protocol is identical to the EmbedIO demo:
    ///   - text frames  -> forwarded to the pty as input bytes
    ///   - binary frames -> JSON command, e.g. { "action": "resize", "data": { "cols": 80, "rows": 30 } }
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

            // Default port 8878; override with the first command line argument or ASPNETCORE_URLS.
            string url = args.Length > 0 ? args[0] : "http://*:8878";
            builder.WebHost.UseUrls(url);

            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseWebSockets();

            app.Map("/terminal", async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await TerminalSession.RunAsync(webSocket, context.RequestAborted);
            });

            Console.WriteLine($"PtyWeb.AspNetCore listening on {url.Replace("*", "localhost", StringComparison.Ordinal)} (WebSocket at /terminal)");

            app.Run();
        }
    }
}
