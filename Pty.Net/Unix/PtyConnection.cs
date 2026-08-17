// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Unix
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    /// A connection to a Unix-style pseudoterminal.
    /// </summary>
    internal abstract class PtyConnection : IPtyConnection
    {
        private const int EINTR = 4;
        private const int ECHILD = 10;
        private const int SignalMask = 127;
        private const int ExitCodeMask = 255;

        private readonly TraceSource trace;
        private readonly int controller;
        private readonly int pid;
        private readonly ManualResetEvent terminalProcessTerminatedEvent = new ManualResetEvent(false);
        private int exitCode;
        private int exitSignal;
        private int exited;
        private int disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PtyConnection"/> class.
        /// </summary>
        /// <param name="controller">The fd of the pty controller.</param>
        /// <param name="pid">The id of the spawned process.</param>
        /// <param name="trace">The tracer to trace execution with.</param>
        public PtyConnection(int controller, int pid, TraceSource trace)
        {
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
            this.ReaderStream = new PtyStream(controller, FileAccess.Read);
            this.WriterStream = new PtyStream(controller, FileAccess.Write);

            this.controller = controller;
            this.pid = pid;
            var childWatcherThread = new Thread(this.ChildWatcherThreadProc)
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest,
                Name = $"Watcher thread for child process {pid}",
            };

            childWatcherThread.Start();
        }

        /// <inheritdoc/>
        public event EventHandler<PtyExitedEventArgs>? ProcessExited;

        /// <inheritdoc/>
        public Stream ReaderStream { get; }

        /// <inheritdoc/>
        public Stream WriterStream { get; }

        /// <inheritdoc/>
        public int Pid => this.pid;

        /// <inheritdoc/>
        public int ExitCode => this.exitCode;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            this.ReaderStream?.Dispose();
            this.WriterStream?.Dispose();

            try
            {
                this.Kill();
            }
            catch (Exception ex)
            {
                // Dispose must not throw; report and continue with cleanup.
                this.trace.TraceEvent(TraceEventType.Warning, 0, $"Killing terminal process {this.pid} during dispose failed: {ex.Message}");
            }

            // The PtyStream wrappers do not own the controller fd (ownsHandle: false),
            // so the fd must be closed explicitly to avoid leaking it in long-running hosts.
            if (close(this.controller) != 0)
            {
                this.trace.TraceEvent(TraceEventType.Warning, 0, $"Closing controller fd {this.controller} failed with errno {Marshal.GetLastWin32Error()}");
            }
        }

        /// <inheritdoc/>
        public void Kill()
        {
            // No-op when the process was already reaped or the connection was disposed.
            if (Volatile.Read(ref this.exited) != 0 || Volatile.Read(ref this.disposed) != 0)
            {
                return;
            }

            if (!this.Kill(this.controller))
            {
                throw new InvalidOperationException($"Killing terminal failed with error {Marshal.GetLastWin32Error()}");
            }
        }

        /// <inheritdoc/>
        public void Resize(int cols, int rows)
        {
            if (!this.Resize(this.controller, cols, rows))
            {
                throw new InvalidOperationException($"Resizing terminal failed with error {Marshal.GetLastWin32Error()}");
            }
        }

        /// <inheritdoc/>
        public bool WaitForExit(int milliseconds)
        {
            return this.terminalProcessTerminatedEvent.WaitOne(milliseconds);
        }

        /// <summary>
        /// OS-specific implementation of the pty-resize function.
        /// </summary>
        /// <param name="controller">The fd of the pty controller.</param>
        /// <param name="cols">The number of columns to resize to.</param>
        /// <param name="rows">The number of rows to resize to.</param>
        /// <returns>True if the function suceeded to resize the pty, false otherwise.</returns>
        protected abstract bool Resize(int controller, int cols, int rows);

        /// <summary>
        /// Kills the terminal process.
        /// </summary>
        /// <param name="controller">The fd of the pty controller.</param>
        /// <returns>True if the function succeeded in killing the process, false otherwise.</returns>
        protected abstract bool Kill(int controller);

        /// <summary>
        /// OS-specific implementation of waiting on the given process id.
        /// </summary>
        /// <param name="pid">The process id to wait on.</param>
        /// <param name="status">The status of the process.</param>
        /// <returns>True if the function succeeded to get the status of the process, false otherwise.</returns>
        protected abstract bool WaitPid(int pid, ref int status);

        /// <summary>
        /// Closes a file descriptor. Resolves to the C library of the current platform
        /// (libc.so.6 on Linux, libc.dylib/libSystem on macOS).
        /// </summary>
        [DllImport("libc", SetLastError = true)]
        private static extern int close(int fd);

        private void ChildWatcherThreadProc()
        {
            this.trace.TraceEvent(TraceEventType.Information, 0, $"Waiting on {this.pid}");

            int status = 0;
            while (true)
            {
                if (this.WaitPid(this.pid, ref status))
                {
                    break;
                }

                int errno = Marshal.GetLastWin32Error();
                if (errno == EINTR)
                {
                    // waitpid(2) was interrupted by a signal; retry.
                    continue;
                }

                if (errno == ECHILD)
                {
                    // waitpid is already handled elsewhere; not an error.
                    this.trace.TraceEvent(TraceEventType.Information, 0, $"waitpid({this.pid}) returned ECHILD; process was already reaped elsewhere.");
                }
                else
                {
                    this.trace.TraceEvent(TraceEventType.Warning, 0, $"waitpid({this.pid}) failed with errno {errno}");
                }

                return;
            }

            this.trace.TraceEvent(TraceEventType.Information, 0, $"Wait succeeded for {this.pid}");
            this.exitSignal = status & SignalMask;
            this.exitCode = this.exitSignal == 0 ? (status >> 8) & ExitCodeMask : 0;
            Volatile.Write(ref this.exited, 1);
            this.terminalProcessTerminatedEvent.Set();
            this.ProcessExited?.Invoke(this, new PtyExitedEventArgs(this.exitCode));
        }
    }
}
