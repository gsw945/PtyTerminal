# PtyTerminal Copilot Instructions

## Project Overview
PtyTerminal is a cross-platform Pseudo Terminal (PTY) library for .NET/C# (netstandard2.1). It abstracts PTY operations across Windows, Linux, and macOS platforms.

**Core Architecture:**
- **Pty.Net**: The library (published as "PTY" NuGet package)
  - Main API: `PtyProvider.SpawnAsync(PtyOptions, CancellationToken)` returns `IPtyConnection`
  - `IPtyConnection`: Provides `ReaderStream`, `WriterStream`, `Pid`, `ExitCode`, `ProcessExited` event
  - Input stream note: `WriterStream` is a PipeStream implementation (internally buffered) — always flush after writing; use the `PtyStreamExtensions.WriteInputAsync` helper (write + flush)
- **examples/**: Self-contained demo applications (code duplication between demos is intentional)
  - `PtyCli` — console demo (raw-mode byte forwarding + resize sync)
  - `PtyWeb.EmbedIO` — WebSocket demo on EmbedIO (default 8877)
  - `PtyWeb.AspNetCore` — WebSocket demo on ASP.NET Core (default 8878)
  - `PtyWeb.SignalR` — SignalR hub demo (default 8879)
  - `PtySession` — session-management demo: sessions outlive the browser connection, 64KB output replay on attach, auto-destroy on pty exit (default 8880)

## Platform-Specific Implementation Pattern

The library uses **runtime platform detection** with lazy-loaded providers:

```
PlatformServices (static class)
├── Detects OS via RuntimeInformation.IsOSPlatform()
├── Returns IPtyProvider for current platform:
│   ├── Windows.PtyProvider (ConPTY or winpty fallback)
│   ├── Linux.PtyProvider (forkpty via P/Invoke to libc.so.6/libutil.so.1)
│   └── Mac.PtyProvider (forkpty via P/Invoke to libSystem.dylib)
```

**Key files to check when adding platform logic:**
- [Pty.Net/PlatformServices.cs](../Pty.Net/PlatformServices.cs) - Platform detection and provider selection
- [Pty.Net/Windows/PtyProvider.cs](../Pty.Net/Windows/PtyProvider.cs) - Windows ConPTY/winpty implementation
- [Pty.Net/Linux/PtyProvider.cs](../Pty.Net/Linux/PtyProvider.cs) - Linux implementation (extends Unix.PtyProvider)
- [Pty.Net/Mac/PtyProvider.cs](../Pty.Net/Mac/PtyProvider.cs) - macOS implementation (extends Unix.PtyProvider)
- [Pty.Net/Unix/PtyProvider.cs](../Pty.Net/Unix/PtyProvider.cs) - Shared Unix base class

## Windows Backend Selection

Windows has **dual backend support** with automatic selection:

```csharp
// In Windows.PtyProvider.StartTerminalAsync():
if (ConPTYConsole.IsPseudoConsoleSupported(options.UseCustomConPtyDll) && !options.ForceWinPty) {
    // Use ConPTY (Windows 10 1809+)
} else {
    // Fallback to WinPty
}
```

**PtyOptions flags:**
- `ForceWinPty`: Override ConPTY detection, force WinPty backend
- `UseCustomConPtyDll`: Load custom conptylib.dll from deps folder (see [Pty.Net/Windows/ConPTYCustomInterop.cs](../Pty.Net/Windows/ConPTYCustomInterop.cs))

## Native Dependencies & NuGet Packaging

**Critical pattern:** Native DLLs (winpty.dll, conpty.dll) are in [Pty.Net/deps/](../Pty.Net/deps/) and **must be copied to consuming applications**.

**Build system integration:**
1. [Pty.Net.csproj](../Pty.Net/Pty.Net.csproj) marks `deps/**/*` as `Content` with `CopyToOutputDirectory=Always`
2. NuGet package includes [build/PTY.targets](../Pty.Net/build/PTY.targets) and [buildTransitive/PTY.targets](../Pty.Net/buildTransitive/PTY.targets)
3. Targets files inject MSBuild tasks to copy deps to consumer's output/publish directories

**When modifying native deps:**
- Update both `build/` and `buildTransitive/` targets
- Test with both direct and transitive package references
- Verify NuGet symbol package (snupkg) validation - no native PDBs allowed (see `DeleteNativePdbsBeforePack` target)

## Environment Variables

Platform-specific environment setup in [Pty.Net/PlatformServices.cs](../Pty.Net/PlatformServices.cs#L20-L36):

**Windows:** Empty (inherits system environment)
**Unix (Linux/Mac):** Sets `TERM=xterm-256color`, clears tmux/screen session vars (`TMUX`, `STY`, `WINDOWID`)

User environment from `PtyOptions.Environment` is **merged** with platform defaults in [PtyProvider.cs](../Pty.Net/PtyProvider.cs#L52-L55):
```csharp
// Empty value removes the variable
environment = MergeEnvironment(PlatformServices.PtyEnvironment, null);
environment = MergeEnvironment(options.Environment, environment);
```

## Demo Applications

Each demo lives in its own project under `examples/` (all net10.0, self-contained):

- **PtyCli** ([examples/PtyCli/Program.cs](../examples/PtyCli/Program.cs)): console demo; raw-mode byte forwarding (`RawConsole.cs`), resize sync; `--winpty` forces the winpty backend
- **PtyWeb.EmbedIO** ([examples/PtyWeb.EmbedIO/Program.cs](../examples/PtyWeb.EmbedIO/Program.cs)): WebSocket + xterm.js on EmbedIO; text frames are pty input, binary frames are JSON commands (e.g. resize); default http://*:8877, WS at /terminal
- **PtyWeb.AspNetCore** ([examples/PtyWeb.AspNetCore/Program.cs](../examples/PtyWeb.AspNetCore/Program.cs)): same frontend/wire protocol as the EmbedIO demo on ASP.NET Core; default http://*:8878, WS at /terminal
- **PtyWeb.SignalR** ([examples/PtyWeb.SignalR/Program.cs](../examples/PtyWeb.SignalR/Program.cs)): SignalR hub at /terminalHub; client invokes `Input(string)`/`Resize(cols, rows)`, server pushes `Output(byte[])`/`Closed(exitCode)`; default http://*:8879
- **PtySession** ([examples/PtySession/Program.cs](../examples/PtySession/Program.cs)): session management over ASP.NET Core WebSockets; binary frames are JSON commands (list/create/attach/detach/destroy/resize), text frames are input, binary frames are output with 64KB replay; sessions auto-destroy when the pty exits; default http://*:8880, WS at /terminal

## Common Patterns

**Spawning a PTY:**
```csharp
var options = new PtyOptions {
    App = "cmd.exe",           // or "bash", "/bin/sh"
    Cols = 80, Rows = 24,
    Cwd = Environment.CurrentDirectory,
    CommandLine = new[] { "/k", "echo hello" },
    Environment = new Dictionary<string, string>()
};
var pty = await PtyProvider.SpawnAsync(options, CancellationToken.None);
```

**Reading/Writing (always flush input):**
```csharp
using var reader = new StreamReader(pty.ReaderStream, Encoding.Default); // System encoding
using var writer = new StreamWriter(pty.WriterStream, Encoding.Default) { AutoFlush = true };
// Or use the extension helper: await pty.WriterStream.WriteInputAsync("echo test");
await writer.WriteLineAsync("echo test");
var output = await reader.ReadLineAsync();
```

## Debugging Tips

- Windows: Check [Pty.Net/Windows/WindowsVersion.cs](../Pty.Net/Windows/WindowsVersion.cs) and [WindowsReleaseInfo.cs](../Pty.Net/Windows/WindowsReleaseInfo.cs) for OS detection issues
- ConPTY loading: See [ConPTYCustomInterop.cs](../Pty.Net/Windows/ConPTYCustomInterop.cs) `LoadCustomConPtyDll()` logic
- P/Invoke errors on Unix: Check `NativeMethods.cs` in Linux/Mac folders
- Input not reaching the pty: you are probably not flushing `WriterStream` — use `WriteInputAsync`
- Run tests: `dotnet build` in solution root, then run any example project

## Conventions

- **Microsoft copyright headers** on all C# files (MIT license)
- **Nullable reference types enabled** (`<Nullable>enable</Nullable>`)
- **XML documentation comments** required for public APIs (`<GenerateDocumentationFile>True</GenerateDocumentationFile>`)
- **TraceSource logging** via `TraceSource trace` parameter (passed through to providers)
- **Platform-specific code** goes in Windows/Linux/Mac folders, **shared Unix logic** in Unix/ folder