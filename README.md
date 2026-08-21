# PtyTerminal

[![PTY](https://img.shields.io/nuget/v/PTY.svg)](https://www.nuget.org/packages/PTY/) [![License](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/license/MIT)

Go port for PtyTerminal: [ptyterminal-go](https://github.com/gsw945/ptyterminal-go)

**English** | [简体中文](./README_zh.md)

cross platform [Pseudo Terminal (PTY)](https://en.wikipedia.org/wiki/Pseudoterminal) Library and Usage Demos in .NET(C#)

## Projects
#### **Pty.Net**  cross platform [Pseudo Terminal (PTY)](https://en.wikipedia.org/wiki/Pseudoterminal) Library in .NET(C#). Modified from [microsoft/vs-pty.net](https://github.com/microsoft/vs-pty.net/tree/main/src/Pty.Net) and inspired by [WindowsGSM/SteamCMD.ConPTY](https://github.com/WindowsGSM/SteamCMD.ConPTY/tree/main/SteamCMD.ConPTY).
- Compatibility with `ConPTY` and `winpty` on Windows Platform
- P/Invoke APIs (`forkpty` `ioctl` `kill` ...) on Unix Platforms
    - APIs provided by `libc.so.6` and `libutil.so.1` for Linux
    - APIs provided by `libSystem.dylib` for MacOs

#### examples
- **PtyCli** Console demo to use Pty.Net
    - Puts the host console into raw input mode and forwards keystrokes as raw bytes (same as the web demos; arrows, function keys, Ctrl/Alt combos and IME input all work)
    - Syncs console window size changes to the pty (resize)
    - Run: `dotnet run --project examples/PtyCli`
![console-demo.png](./assets/console-demo.png)
- **PtyWeb.EmbedIO** Web demo to use Pty.Net, powered by [EmbedIO](https://github.com/unosquare/embedio) and [Xterm.js](https://github.com/xtermjs/xterm.js/)
    - Run: `dotnet run --project examples/PtyWeb.EmbedIO` (default <http://localhost:8877>)
![web-demo-01.png](./assets/web-demo-01.png)
![web-demo-02.png](./assets/web-demo-02.png)
- **PtyWeb.AspNetCore** WebSocket server demo based on [WebSockets support in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets)
    - Same frontend and wire protocol as the EmbedIO demo: text frames are pty input, binary frames are JSON commands (e.g. resize)
    - Run: `dotnet run --project examples/PtyWeb.AspNetCore` (default <http://localhost:8878>, WebSocket at `/terminal`)
- **PtyWeb.SignalR** Server demo based on [ASP.NET Core SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction)
    - Hub methods: client calls `Input(string)` / `Resize(cols, rows)`; server pushes `Output(byte[])` / `Closed(exitCode)`
    - Run: `dotnet run --project examples/PtyWeb.SignalR` (default <http://localhost:8879>, hub at `/terminalHub`)
- **PtySession** pty session management demo based on [WebSockets support in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets)
    - Sessions outlive the browser connection: closing the page keeps the pty running on the server; reopen the page, pick the session from the list and resume xterm interaction (64KB output replay on attach)
    - Sessions are destroyed explicitly by the client, or automatically when the pty exits (e.g. typing exit)
    - Wire protocol: binary frames are JSON commands (list/create/attach/detach/destroy/resize), text frames are pty input, binary frames are pty output (including replay)
    - Run: `dotnet run --project examples/PtySession` (default <http://localhost:8880>, WebSocket at `/terminal`)

#### Tips
- **Always flush input**: WriterStream is a PipeStream implementation (internally buffered); call FlushAsync after writing or the input will not reach the pty. Use the PtyStreamExtensions.WriteInputAsync helper (write + flush) to avoid losing input.

- Each example project is self-contained (code duplication is intentional) so it can be read and modified independently
- To exit PtyCli, exit the shell inside the pty normally (e.g. type `exit`); Ctrl+C is forwarded to the pty child program as a key press
- Default ports: EmbedIO `8877`, AspNetCore `8878`, SignalR `8879`, PtySession `8880`; all can be overridden with the first command line argument, e.g. `dotnet run --project examples/PtyWeb.SignalR -- http://*:9000`

## Reference
- [Windows Command-Line: Introducing the Windows Pseudo Console (ConPTY)](https://devblogs.microsoft.com/commandline/windows-command-line-introducing-the-windows-pseudo-console-conpty/)
- Github: [rprichard/winpty](https://github.com/rprichard/winpty)
- Github: [microsoft/terminal](https://github.com/microsoft/terminal)
    - [src/winconpty](https://github.com/microsoft/terminal/tree/main/src/winconpty)
    - [samples/ConPTY](https://github.com/microsoft/terminal/tree/main/samples/ConPTY)
        - [MiniTerm](https://github.com/microsoft/terminal/tree/main/samples/ConPTY/MiniTerm/MiniTerm)
- Github [microsoft/node-pty](https://github.com/microsoft/node-pty)
- Github: [unosquare/embedio](https://github.com/unosquare/embedio)
    - [EmbedIO - WebSockets Example](https://unosquare.github.io/embedio/#websockets-example)
- Github: [xtermjs/xterm.js](https://github.com/xtermjs/xterm.js)
    - [XTERM.JS - Addons/attach](https://xtermjs.org/docs/api/addons/attach/)
- [WebSockets support in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets)
- [ASP.NET Core SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction)
- [Platform Invoke (P/Invoke)](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)
