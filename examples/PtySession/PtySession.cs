using Pty.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace PtySession
{
    /// <summary>
    /// One pty session. It lives independently of any browser connection:
    /// when the WebSocket closes the pty keeps running and its output is
    /// buffered until a client attaches again.
    /// </summary>
    public class PtySession : IDisposable
    {
        // How much pty output is buffered for replay on attach.
        private const int HistorySize = 64 * 1024;

        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly List<byte> history = new List<byte>();
        private SemaphoreSlim sendLock = new SemaphoreSlim(1, 1); // replaced by the connection's lock on Attach
        private readonly object clientLock = new object();

        private WebSocket? client;
        private bool exited;
        private int exitCode;

        /// <summary>
        /// Fired when the pty process exits (e.g. the client typed `exit`).
        /// </summary>
        public event Action<PtySession>? Exited;

        public PtySession(string id, string name, IPtyConnection terminal)
        {
            Id = id;
            Name = name;
            Terminal = terminal;
            Created = DateTime.UtcNow;

            terminal.ProcessExited += (_, e) =>
            {
                exited = true;
                exitCode = e.ExitCode;
                _ = NotifyAsync(new { type = "session-ended", data = new { id = Id, exitCode } });
                Exited?.Invoke(this);
                cts.Cancel();
            };

            _ = PumpAsync(cts.Token);
        }

        public string Id { get; }

        public string Name { get; }

        public DateTime Created { get; }

        public IPtyConnection Terminal { get; }

        public bool Running => !exited;

        public int ExitCode => exitCode;

        public long CreatedMs => new DateTimeOffset(Created).ToUnixTimeMilliseconds();

        /// <summary>
        /// Binds a client to this session and returns the buffered history to replay.
        /// A previous client is detached and notified. The connection send lock is
        /// shared with the connection's own command responses so that the output
        /// pump and the read loop never write to the socket concurrently.
        /// </summary>
        public byte[] Attach(WebSocket newClient, SemaphoreSlim connectionSendLock)
        {
            WebSocket? old;
            lock (clientLock)
            {
                old = client;
                client = newClient;
            }

            if (connectionSendLock != null)
            {
                sendLock = connectionSendLock;
            }

            if (old != null && old != newClient)
            {
                _ = SendAsync(old, new { type = "detached", data = new { id = Id, reason = "attached elsewhere" } });
            }

            byte[] snapshot;
            lock (history)
            {
                snapshot = history.ToArray();
            }

            return snapshot;
        }

        /// <summary>
        /// Unbinds the current client without destroying the session.
        /// </summary>
        public void Detach(WebSocket? context)
        {
            lock (clientLock)
            {
                if (context == null || client == context)
                {
                    client = null;
                }
            }
        }

        public async Task SendInputAsync(byte[] data)
        {
            // PipeStream buffers internally; flush so the bytes actually reach
            // the pty (the production demo uses StreamWriter + AutoFlush for
            // the same reason).
            await Terminal.WriterStream.WriteInputAsync(data);
        }

        public void Resize(int cols, int rows)
        {
            Terminal.Resize(cols, rows);
        }

        public void Destroy()
        {
            cts.Cancel();
            Terminal.Kill();
        }

        public void Dispose()
        {
            cts.Cancel();
            Terminal.Dispose();
            // NOTE: sendLock is intentionally not disposed here: the output
            // pump may still be sending concurrently and would throw
            // ObjectDisposedException. It is garbage collected with the session.
        }

        public object ToInfo()
        {
            return new
            {
                id = Id,
                name = Name,
                created = CreatedMs,
                running = Running,
                exitCode = exited ? exitCode : 0,
            };
        }

        /// <summary>
        /// Reads pty output, buffers it and forwards it to the attached client.
        /// </summary>
        private async Task PumpAsync(CancellationToken ct)
        {
            var buffer = new byte[8192];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int count;
                    try
                    {
                        count = await Terminal.ReaderStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        // The pty side closed.
                        break;
                    }

                    if (count <= 0)
                    {
                        break;
                    }

                    lock (history)
                    {
                        history.AddRange(buffer.AsSpan(0, count).ToArray());
                        if (history.Count > HistorySize)
                        {
                            history.RemoveRange(0, history.Count - HistorySize);
                        }
                    }

                    var target = GetClient();
                    if (target != null)
                    {
                        var chunk = buffer.AsSpan(0, count).ToArray();
                        await SendBinaryAsync(target, chunk);
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.DebugWriteLine($"{ex.GetType()}: {ex.Message}");
            }
            finally
            {
                exited = true;
                exitCode = Terminal.ExitCode;
            }
        }

        private async Task NotifyAsync(object message)
        {
            var target = GetClient();
            if (target != null)
            {
                await SendAsync(target, message);
            }
        }

        private WebSocket? GetClient()
        {
            lock (clientLock)
            {
                return client;
            }
        }

        private async Task SendBinaryAsync(WebSocket ws, byte[] data)
        {
            await sendLock.WaitAsync();
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            finally
            {
                sendLock.Release();
            }
        }

        private async Task SendAsync(WebSocket ws, object message)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(message);
            var data = System.Text.Encoding.UTF8.GetBytes(json);
            await sendLock.WaitAsync();
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            finally
            {
                sendLock.Release();
            }
        }
    }
}
