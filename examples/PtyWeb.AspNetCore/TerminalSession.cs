using Pty.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PtyWeb.AspNetCore
{
    /// <summary>
    /// Runs one pty session per WebSocket connection: pumps pty output to the client
    /// as binary frames and forwards client messages to the pty (text = input bytes,
    /// binary = JSON command such as resize).
    /// </summary>
    public static class TerminalSession
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        public static async Task RunAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var terminal = await SpawnTerminalAsync(cts.Token);

            // On pty exit, stop the output pump shortly afterwards: on Windows the
            // ConPTY host may keep the output pipe open until the pseudo console
            // handle is closed, so EOF alone is not a reliable end signal. The grace
            // period lets any trailing output drain first.
            using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            terminal.ProcessExited += (_, e) =>
            {
                Console.WriteLine($"[terminal {terminal.Pid}] exited with code {e.ExitCode}");
                drainCts.CancelAfter(500);
            };

            var outputTask = PumpOutputAsync(webSocket, terminal, drainCts.Token);
            var inputTask = PumpInputAsync(webSocket, terminal, cts.Token);

            // End when either direction closes (client disconnected or pty exited).
            await Task.WhenAny(outputTask, inputTask);
            cts.Cancel();

            try
            {
                await Task.WhenAll(outputTask, inputTask);
            }
            catch
            {
                // Best effort drain; the session is ending.
            }

            await CloseWebSocketAsync(webSocket);
        }

        private static async Task<IPtyConnection> SpawnTerminalAsync(CancellationToken cancellationToken)
        {
            bool isWin = OperatingSystem.IsWindows();
            string app = isWin ? Path.Combine(Environment.SystemDirectory, "cmd.exe") : "/usr/bin/bash";
            var options = new PtyOptions
            {
                Name = "Custom terminal",
                Rows = 30,
                Cols = 80,
                Cwd = Environment.CurrentDirectory,
                App = app,
                CommandLine = isWin ? Array.Empty<string>() : new[] { "--login" },
                VerbatimCommandLine = false,
                Environment = new Dictionary<string, string>
                {
                    { "FOO", "bar" },
                    { "LANG", isWin ? string.Empty : "en_US.UTF-8" },
                },
            };

            return await PtyProvider.SpawnAsync(options, cancellationToken);
        }

        private static async Task PumpOutputAsync(WebSocket webSocket, IPtyConnection terminal, CancellationToken ct)
        {
            var buffer = new byte[8192];
            Task<int>? pendingRead = null;
            while (!ct.IsCancellationRequested)
            {
                pendingRead ??= terminal.ReaderStream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);
                if (await Task.WhenAny(pendingRead, Task.Delay(200)) == pendingRead)
                {
                    int count;
                    try
                    {
                        count = await pendingRead;
                    }
                    catch (IOException)
                    {
                        // The pty side closed.
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    pendingRead = null;
                    if (count <= 0)
                    {
                        // The pty exited and its output was fully drained.
                        break;
                    }

                    try
                    {
                        await webSocket.SendAsync(buffer.AsMemory(0, count), WebSocketMessageType.Binary, endOfMessage: true, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (WebSocketException)
                    {
                        break;
                    }
                }
            }
        }

        private static async Task PumpInputAsync(WebSocket webSocket, IPtyConnection terminal, CancellationToken ct)
        {
            var buffer = new byte[4096];
            Task<WebSocketReceiveResult>? pendingReceive = null;
            while (!ct.IsCancellationRequested)
            {
                // NOTE: the receive itself must never be cancelled. Cancelling a
                // pending ReceiveAsync on a Kestrel WebSocket puts the socket into the
                // Aborted state and breaks the subsequent close handshake. The pump is
                // stopped via the loop condition instead, and the (single) pending
                // receive is simply abandoned when the session ends.
                pendingReceive ??= webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), CancellationToken.None);
                if (await Task.WhenAny(pendingReceive, Task.Delay(200)) == pendingReceive)
                {
                    WebSocketReceiveResult result;
                    try
                    {
                        result = await pendingReceive;
                    }
                    catch (WebSocketException)
                    {
                        break;
                    }

                    pendingReceive = null;
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.Count == 0)
                    {
                        continue;
                    }

                    if (result.MessageType == WebSocketMessageType.Binary && TryHandleCommand(buffer, result.Count, terminal))
                    {
                        continue;
                    }

                    await terminal.WriterStream.WriteInputAsync(buffer.AsMemory(0, result.Count), ct);
                }
            }
        }

        /// <summary>
        /// Parses a binary command frame. Returns true when the frame was a recognized
        /// command (e.g. resize); false when it should be treated as raw input.
        /// </summary>
        private static bool TryHandleCommand(byte[] buffer, int count, IPtyConnection terminal)
        {
            try
            {
                using var document = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, count));
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("action", out JsonElement actionElement))
                {
                    return false;
                }

                string? action = actionElement.GetString();
                if (!string.Equals(action, "resize", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!root.TryGetProperty("data", out JsonElement data) ||
                    !data.TryGetProperty("cols", out JsonElement colsElement) ||
                    !data.TryGetProperty("rows", out JsonElement rowsElement))
                {
                    return false;
                }

                int cols = colsElement.GetInt32();
                int rows = rowsElement.GetInt32();
                if (cols > 0 && rows > 0 && cols <= 500 && rows <= 500)
                {
                    terminal.Resize(cols, rows);
                }

                return true;
            }
            catch (JsonException)
            {
                // Not a command frame; treat as raw input.
                return false;
            }
        }

        private static async Task CloseWebSocketAsync(WebSocket webSocket)
        {
            if (webSocket.State != WebSocketState.Open)
            {
                return;
            }

            try
            {
                // Complete close handshake: send the close frame and wait for the
                // client's acknowledgement (browsers acknowledge automatically).
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "pty session ended", CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[session] close handshake failed: {ex.GetType()}: {ex.Message}");
                try
                {
                    webSocket.Abort();
                }
                catch
                {
                }
            }
        }
    }
}
