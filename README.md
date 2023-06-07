## PtyTerminal

**English** | [¼òÌåÖÐÎÄ](./README_zh.md)

cross platform [Pseudo Terminal (PTY)](https://en.wikipedia.org/wiki/Pseudoterminal) Library and Usage Demos in .NET(C#)

## Projects
- **Pty.Net**  cross platform [Pseudo Terminal (PTY)](https://en.wikipedia.org/wiki/Pseudoterminal) Library in .NET(C#)
    - Compatibility with `ConPTY` and `winpty` on Windows Platform
    - P/Invoke APIs (`forkpty`¡¢`ioctl`¡¢`kill` ...) on Unix Platforms
        - APIs provided by `libc.so.6` and `libutil.so.1` for Linux
        - APIs provided by `libSystem.dylib` for MacOs
- **PtyWeb**
    - **CliDemo** Console demo to use Pty.Net
    - **WebDemo** Web demo to use Pty.Net, powered by [EmbedIO](https://github.com/unosquare/embedio) and [Xterm.js](https://github.com/xtermjs/xterm.js/)
        - TODO: Replace EmbedIO with [WebSockets support in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets)

## Reference
- [Windows Command-Line: Introducing the Windows Pseudo Console (ConPTY)](https://devblogs.microsoft.com/commandline/windows-command-line-introducing-the-windows-pseudo-console-conpty/)
- Github: [rprichard/winpty](https://github.com/rprichard/winpty)
- Github: [microsoft/terminal](https://github.com/microsoft/terminal)
    - [src/winconpty](https://github.com/microsoft/terminal/tree/main/src/winconpty)
    - [samples/ConPTY](https://github.com/microsoft/terminal/tree/main/samples/ConPTY)
- [Platform Invoke (P/Invoke)](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)
