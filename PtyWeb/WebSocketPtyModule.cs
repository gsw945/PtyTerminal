using EmbedIO.WebSockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PtyWeb
{
    public class WebSocketPtyModule : WebSocketModule
    {
        public WebSocketPtyModule(string urlPath, bool enableConnectionWatchdog = true) : base(urlPath, enableConnectionWatchdog)
        {
        }

        protected override Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            return SendToOthersAsync(context, Encoding.GetString(buffer));
        }

        protected override Task OnClientConnectedAsync(IWebSocketContext context)
        {
            // return base.OnClientConnectedAsync(context);
            return Task.WhenAll(
                base.OnClientConnectedAsync(context),
                SendAsync(context, "Welcome to the chat room!"),
                SendToOthersAsync(context, "Someone joined the chat room.")
            );
        }

        protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
        {
            // return base.OnClientDisconnectedAsync(context);
            return Task.WhenAll(
                SendToOthersAsync(context, "Someone left the chat room."),
                base.OnClientDisconnectedAsync(context)
            );
        }

        private Task SendToOthersAsync(IWebSocketContext context, string payload)
        {
            return BroadcastAsync(payload, c => c != context);
        }
    }
}
