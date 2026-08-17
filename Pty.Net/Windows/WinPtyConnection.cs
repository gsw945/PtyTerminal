// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net.Windows
{
    using Pty.Net.Windows.Native;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using static Pty.Net.Windows.WinptyNativeInterop;

    /// <summary>
    /// A connection to a pseudoterminal spawned via winpty.
    /// </summary>
    internal class WinPtyConnection : IPtyConnection
    {
        private readonly IntPtr handle;
        private readonly Kernel32.SafeProcessHandle processHandle;
        private readonly Process process;
        private int disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WinPtyConnection"/> class.
        /// </summary>
        /// <param name="readerStream">The reading side of the pty connection.</param>
        /// <param name="writerStream">The writing side of the pty connection.</param>
        /// <param name="handle">A handle to the winpty instance.</param>
        /// <param name="processHandle">A handle to the spawned process.</param>
        public WinPtyConnection(Stream readerStream, Stream writerStream, IntPtr handle, Kernel32.SafeProcessHandle processHandle)
        {
            this.ReaderStream = readerStream;
            this.WriterStream = writerStream;
            this.Pid = Kernel32.GetProcessId(processHandle);

            this.handle = handle;
            this.processHandle = processHandle;
            this.process = Process.GetProcessById(this.Pid);
            this.process.EnableRaisingEvents = true;
            this.process.Exited += this.Process_Exited;
        }

        /// <inheritdoc/>
        public event EventHandler<PtyExitedEventArgs>? ProcessExited;

        /// <inheritdoc/>
        public Stream ReaderStream { get; }

        /// <inheritdoc/>
        public Stream WriterStream { get; }

        /// <inheritdoc/>
        public int Pid { get; }

        /// <inheritdoc/>
        public int ExitCode => this.process.HasExited ? this.process.ExitCode : 0;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            this.ReaderStream?.Dispose();
            this.WriterStream?.Dispose();

            this.process.Exited -= this.Process_Exited;
            this.process.Dispose();

            this.processHandle.Close();
            winpty_free(this.handle);
        }

        /// <inheritdoc/>
        public void Kill()
        {
            if (!this.process.HasExited)
            {
                this.process.Kill();
            }
        }

        /// <inheritdoc/>
        public void Resize(int cols, int rows)
        {
            winpty_set_size(this.handle, cols, rows, out IntPtr err);
            if (err != IntPtr.Zero)
            {
                string message = $"Resizing winpty terminal failed: {winpty_error_msg(err)} ({winpty_error_code(err)})";
                winpty_error_free(err);
                throw new InvalidOperationException(message);
            }
        }

        /// <inheritdoc/>
        public bool WaitForExit(int milliseconds)
        {
            return this.process.WaitForExit(milliseconds);
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            this.ProcessExited?.Invoke(this, new PtyExitedEventArgs(this.process.HasExited ? this.process.ExitCode : 0));
        }
    }
}
