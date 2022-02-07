using EmbedIO.WebSockets;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace PtyWeb
{
    public class WebSocketPtyModule : WebSocketModule
    {
        private ConcurrentDictionary<string, WebTerminal> terminals;

        public WebSocketPtyModule(string urlPath, bool enableConnectionWatchdog = true) : base(urlPath, enableConnectionWatchdog)
        {
            terminals = new ConcurrentDictionary<string, WebTerminal>();
        }

        public async Task Send2ClientAsync(IWebSocketContext context, byte[] data)
        {
            await SendAsync(context, data);
        }

        public async Task CloseClientAsync(IWebSocketContext context)
        {
            await CloseAsync(context);
        }

        protected override Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            if (terminals.TryGetValue(context.Id, out var terminal))
            {
                if (result.MessageType == (int)System.Net.WebSockets.WebSocketMessageType.Text)
                {
                    // xterm-AttachAddon发送的数据均为 Text 类型
                    // return terminal.SendDataAsync(buffer);
                }
                else if (result.MessageType == (int)System.Net.WebSockets.WebSocketMessageType.Binary)
                {
                    // Binary 类型为自定义指令
                    // TODO: 指令解析
                    // terminal.Resize(1, 2);
                    // return Task.CompletedTask;
                    // return terminal.SendDataAsync(buffer);
                }
                return terminal.SendDataAsync(buffer);
            }
            return Task.CompletedTask;
            // return SendToOthersAsync(context, Encoding.GetString(buffer));
        }

        protected override Task OnClientConnectedAsync(IWebSocketContext context)
        {
            if (terminals.TryAdd(context.Id, new WebTerminal(context, this)))
            {
                Task.Run(terminals[context.Id].Run, terminals[context.Id].CTS.Token);
            }
            return base.OnClientConnectedAsync(context);
        }

        protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
        {
            if (terminals.TryRemove(context.Id, out var terminal))
            {
                terminal.CTS.Cancel();
            }
            return base.OnClientDisconnectedAsync(context);
        }

        private Task SendToOthersAsync(IWebSocketContext context, string payload)
        {
            return BroadcastAsync(payload, c => c != context);
        }
    }
}
