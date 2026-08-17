using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PtySession
{
    /// <summary>
    /// pty-session: pty session management over ASP.NET Core WebSocket.
    ///
    /// Sessions outlive the browser connection (closing the page keeps the pty
    /// running); a later visit can list, attach and resume any session. A
    /// session is destroyed explicitly by the client, or automatically when the
    /// pty process exits (e.g. typing `exit`).
    ///
    /// Wire protocol (binary frames are JSON commands, text frames are pty input
    /// while attached):
    ///   client -> server: {"action":"list"} / {"action":"create","data":{cols,rows}}
    ///     {"action":"attach","data":{"id"}} / {"action":"detach"}
    ///     {"action":"destroy","data":{"id"}} / {"action":"resize","data":{cols,rows}}
    ///     <text frame> -> input to the attached session
    ///   server -> client: {"type":"list"|"created"|"attached"|"destroyed"|"detached"|"session-ended"|"error",...}
    ///     <binary frame> -> pty output (live or replayed on attach)
    /// </summary>
    public static class Program
    {
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

        public static void Main(string[] args)
        {
            // Serve wwwroot from the source tree when running in the project
            // directory (dotnet run); otherwise fall back to the copy next to
            // the executable (direct exe launch).
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

            // Default port 8880; override with the first command line argument.
            string url = args.Length > 0 ? args[0] : "http://*:8880";
            builder.WebHost.UseUrls(url);

            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseWebSockets();

            var manager = new SessionManager();

            app.Map("/terminal", async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await HandleConnectionAsync(webSocket, manager, context.RequestAborted);
            });

            Console.WriteLine($"PtySession listening on {url.Replace("*", "localhost", StringComparison.Ordinal)} (WebSocket at /terminal)");
            app.Run();
        }

        private static async Task HandleConnectionAsync(WebSocket ws, SessionManager manager, CancellationToken ct)
        {
            // Shared by the read loop's responses and the attached session's
            // output pump: a WebSocket must never be written concurrently.
            var sendLock = new SemaphoreSlim(1, 1);
            PtySession? current = null;
            var buffer = new byte[4096];

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    try
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    }
                    catch (WebSocketException)
                    {
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.Count == 0)
                    {
                        continue;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Terminal input for the attached session.
                        if (current != null)
                        {
                            await current.SendInputAsync(buffer.AsSpan(0, result.Count).ToArray());
                        }

                        continue;
                    }

                    // Binary frame: JSON command.
                    PtySessionAction<PtySessionActionData>? command = null;
                    try
                    {
                        command = JsonSerializer.Deserialize<PtySessionAction<PtySessionActionData>>(
                            Encoding.UTF8.GetString(buffer, 0, result.Count), JsonOpts);
                    }
                    catch (JsonException)
                    {
                    }

                    if (command == null)
                    {
                        await SendJsonAsync(ws, sendLock, new { type = "error", data = "bad command" });
                        continue;
                    }

                    switch (command.action)
                    {
                        case PtySessionAction<PtySessionActionData>.ActionType.list:
                            await SendJsonAsync(ws, sendLock, new { type = "list", data = manager.List() });
                            break;

                        case PtySessionAction<PtySessionActionData>.ActionType.create:
                        {
                            var cols = command.data?.cols ?? 0;
                            var rows = command.data?.rows ?? 0;
                            if (cols <= 0) cols = 80;
                            if (rows <= 0) rows = 30;

                            var session = await manager.CreateAsync(cols, rows, CancellationToken.None);
                            DetachCurrent(ref current, ws);
                            current = session;
                            var history = session.Attach(ws, sendLock);

                            await SendJsonAsync(ws, sendLock, new { type = "created", data = new { id = session.Id, name = session.Name } });
                            if (history.Length > 0)
                            {
                                await SendBinaryAsync(ws, sendLock, history);
                            }

                            break;
                        }

                        case PtySessionAction<PtySessionActionData>.ActionType.attach:
                        {
                            if (command.data == null || string.IsNullOrEmpty(command.data.id))
                            {
                                await SendJsonAsync(ws, sendLock, new { type = "error", data = "missing id" });
                                break;
                            }

                            var session = manager.Get(command.data.id);
                            if (session == null)
                            {
                                await SendJsonAsync(ws, sendLock, new { type = "error", data = "session not found" });
                                break;
                            }

                            DetachCurrent(ref current, ws);
                            current = session;
                            var history = session.Attach(ws, sendLock);

                            await SendJsonAsync(ws, sendLock, new { type = "attached", data = new { id = session.Id, name = session.Name, running = session.Running } });
                            if (history.Length > 0)
                            {
                                await SendBinaryAsync(ws, sendLock, history);
                            }

                            break;
                        }

                        case PtySessionAction<PtySessionActionData>.ActionType.detach:
                            current?.Detach(ws);
                            current = null;
                            await SendJsonAsync(ws, sendLock, new { type = "detached", data = new { reason = "client detach" } });
                            break;

                        case PtySessionAction<PtySessionActionData>.ActionType.destroy:
                        {
                            if (command.data == null || string.IsNullOrEmpty(command.data.id))
                            {
                                await SendJsonAsync(ws, sendLock, new { type = "error", data = "missing id" });
                                break;
                            }

                            if (!manager.Destroy(command.data.id))
                            {
                                await SendJsonAsync(ws, sendLock, new { type = "error", data = "session not found" });
                                break;
                            }

                            if (current != null && current.Id == command.data.id)
                            {
                                current = null;
                            }

                            await SendJsonAsync(ws, sendLock, new { type = "destroyed", data = new { id = command.data.id } });
                            break;
                        }

                        case PtySessionAction<PtySessionActionData>.ActionType.resize:
                            if (current != null &&
                                command.data != null &&
                                command.data.cols > 0 && command.data.rows > 0 &&
                                command.data.cols <= 500 && command.data.rows <= 500)
                            {
                                current.Resize(command.data.cols, command.data.rows);
                            }

                            break;

                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.DebugWriteLine($"{ex.GetType()}: {ex.Message}");
            }
            finally
            {
                // The browser closed; keep the session running on the server.
                current?.Detach(ws);

                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    }
                    catch
                    {
                        // The client may already be gone.
                    }
                }
            }
        }

        private static void DetachCurrent(ref PtySession? current, WebSocket ws)
        {
            current?.Detach(ws);
            current = null;
        }

        private static Task SendJsonAsync(WebSocket ws, SemaphoreSlim sendLock, object message)
        {
            var json = JsonSerializer.Serialize(message);
            return SendAsync(ws, sendLock, Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text);
        }

        private static Task SendBinaryAsync(WebSocket ws, SemaphoreSlim sendLock, byte[] data)
        {
            return SendAsync(ws, sendLock, data, WebSocketMessageType.Binary);
        }

        private static async Task SendAsync(WebSocket ws, SemaphoreSlim sendLock, byte[] data, WebSocketMessageType type)
        {
            await sendLock.WaitAsync();
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(data), type, true, CancellationToken.None);
            }
            finally
            {
                sendLock.Release();
            }
        }
    }
}
