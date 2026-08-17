using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace PtyWeb.SignalR
{
    /// <summary>
    /// SignalR hub: one pty session per connection.
    /// </summary>
    public class TerminalHub : Hub
    {
        private readonly TerminalSessionManager sessions;

        public TerminalHub(TerminalSessionManager sessions)
        {
            this.sessions = sessions;
        }

        /// <summary>
        /// Forwards keystrokes/text from the client to the pty (UTF-8 encoded).
        /// </summary>
        public Task Input(string data)
            => this.sessions.SendInputAsync(this.Context.ConnectionId, data);

        /// <summary>
        /// Resizes the pty to the client's terminal size.
        /// </summary>
        public Task Resize(int cols, int rows)
            => this.sessions.ResizeAsync(this.Context.ConnectionId, cols, rows);

        public override Task OnConnectedAsync()
        {
            this.sessions.Start(this.Context.ConnectionId, this.Clients.Caller);
            return base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await this.sessions.StopAsync(this.Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
