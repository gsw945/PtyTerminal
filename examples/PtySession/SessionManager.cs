using Pty.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PtySession
{
    /// <summary>
    /// Owns all pty sessions. Sessions outlive their WebSocket connection:
    /// destroying happens either explicitly (client command) or when the pty
    /// process exits (e.g. typing `exit`).
    /// </summary>
    public class SessionManager
    {
        private readonly ConcurrentDictionary<string, PtySession> sessions = new ConcurrentDictionary<string, PtySession>();
        private int nextId;

        public IReadOnlyList<object> List()
        {
            return sessions.Values
                .OrderBy(s => s.CreatedMs)
                .Select(s => s.ToInfo())
                .ToList();
        }

        public PtySession? Get(string id)
        {
            return sessions.TryGetValue(id, out var session) ? session : null;
        }

        public async Task<PtySession> CreateAsync(int cols, int rows, CancellationToken cancellationToken)
        {
            int id = Interlocked.Increment(ref nextId);
            var sessionId = $"sess-{id}";
            var name = $"session #{id}";

            var terminal = await SpawnTerminalAsync(cols, rows, cancellationToken);
            var session = new PtySession(sessionId, name, terminal);

            if (!sessions.TryAdd(sessionId, session))
            {
                session.Dispose();
                throw new InvalidOperationException($"session {sessionId} already exists");
            }

            // When the pty exits (e.g. `exit` typed by the client), remove the
            // session automatically.
            session.Exited += OnSessionExited;
            return session;
        }

        private void OnSessionExited(PtySession session)
        {
            sessions.TryRemove(session.Id, out _);
        }

        public bool Destroy(string id)
        {
            if (!sessions.TryRemove(id, out var session))
            {
                return false;
            }

            session.Exited -= OnSessionExited;
            session.Destroy();
            session.Dispose();
            return true;
        }

        private static async Task<IPtyConnection> SpawnTerminalAsync(int cols, int rows, CancellationToken cancellationToken)
        {
            var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var bash = @"/usr/bin/bash";
            string app = Utils.IsWin ? cmd : bash;
            var options = new PtyOptions()
            {
                Name = "PtySession",
                Rows = rows,
                Cols = cols,
                Cwd = Environment.CurrentDirectory,
                App = app,
                CommandLine = Utils.IsWin ? new string[] { } : new string[] { "--login" },
                VerbatimCommandLine = false,
                Environment = new Dictionary<string, string>()
                {
                    { "FOO", "bar" },
                    { "LANG", Utils.IsWin ? string.Empty : "en_US.UTF-8" },
                },
            };

            return await PtyProvider.SpawnAsync(options, cancellationToken);
        }
    }
}
