using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace PtyWeb.SignalR
{
    /// <summary>
    /// Tracks the pty session of every connected SignalR client.
    /// </summary>
    public sealed class TerminalSessionManager
    {
        private readonly ConcurrentDictionary<string, TerminalSession> sessions = new ConcurrentDictionary<string, TerminalSession>();
        private readonly ILogger<TerminalSessionManager> logger;

        public TerminalSessionManager(ILogger<TerminalSessionManager> logger)
        {
            this.logger = logger;
        }

        public void Start(string connectionId, ISingleClientProxy client)
        {
            // The session is registered immediately and the pty spawns in the
            // background; input sent before the spawn completes is queued.
            var session = TerminalSession.Create(client);
            if (!this.sessions.TryAdd(connectionId, session))
            {
                _ = session.DisposeAsync();
            }
        }

        public async Task SendInputAsync(string connectionId, string data)
        {
            if (this.sessions.TryGetValue(connectionId, out var session))
            {
                try
                {
                    await session.SendInputAsync(data, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to forward input for {ConnectionId}", connectionId);
                }
            }
        }

        public Task ResizeAsync(string connectionId, int cols, int rows)
        {
            if (this.sessions.TryGetValue(connectionId, out var session))
            {
                return session.ResizeAsync(cols, rows);
            }

            Console.WriteLine($"[resize] DROPPED (no session for {connectionId}) cols={cols} rows={rows}");
            return Task.CompletedTask;
        }

        public async Task StopAsync(string connectionId)
        {
            if (this.sessions.TryRemove(connectionId, out var session))
            {
                await session.DisposeAsync();
            }
        }
    }
}
