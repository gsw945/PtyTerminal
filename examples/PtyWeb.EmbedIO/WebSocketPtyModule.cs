using EmbedIO.WebSockets;
using Swan.Formatters;
using Swan.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PtyWeb.EmbedIO
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
            Utils.DebugWriteLine($"OnMessageReceived: {context.Id}, type={result.MessageType}, len={buffer.Length}");
            if (terminals.TryGetValue(context.Id, out var terminal))
            {
                if (result.MessageType == (int)System.Net.WebSockets.WebSocketMessageType.Binary)
                {
                    // Binary 类型为自定义指令(如 resize)
                    try
                    {
                        var strData = System.Text.Encoding.UTF8.GetString(buffer);
                        var ptyWebAction = Json.Deserialize<PtyWebAction<Dictionary<string, int>>>(strData);
                        if (ptyWebAction != null)
                        {
                            switch (ptyWebAction.action)
                            {
                                case PtyWebAction<Dictionary<string, int>>.ActionType.resize:
                                    {
                                        if (
                                            ptyWebAction.data.TryGetValue("cols", out var cols) &&
                                            ptyWebAction.data.TryGetValue("rows", out var rows) &&
                                            cols > 0 && rows > 0 &&
                                            cols <= 500 && rows <= 500
                                        )
                                        {
                                            terminal.Resize(cols, rows);
                                        }
                                    }

                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ex.Debug(nameof(WebSocketPtyModule), ex.Message);
                    }

                    return Task.CompletedTask;
                }

                // xterm-AttachAddon发送的数据(文本帧)
                return terminal.SendDataAsync(buffer).AsTask();
            }

            return Task.CompletedTask;
        }

        protected override Task OnClientConnectedAsync(IWebSocketContext context)
        {
            Utils.DebugWriteLine($"OnClientConnected: {context.Id}");
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
    }
}
