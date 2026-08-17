// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Extensions for writing input to a pty connection.
    /// </summary>
    public static class PtyStreamExtensions
    {
        /// <summary>
        /// Writes input bytes to the pty and flushes them.
        /// The writer streams returned by <see cref="IPtyConnection.WriterStream"/> are
        /// PipeStream-based and buffer internally, so a flush is required for the bytes
        /// to actually reach the pty. Callers that do not flush may lose input.
        /// </summary>
        /// <param name="writer">The pty writer stream.</param>
        /// <param name="data">The bytes to write.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public static async Task WriteInputAsync(this Stream writer, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            await writer.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes a UTF-8 encoded string to the pty and flushes it.
        /// </summary>
        /// <param name="writer">The pty writer stream.</param>
        /// <param name="text">The text to write.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public static Task WriteInputAsync(this Stream writer, string text, CancellationToken cancellationToken = default)
        {
            return WriteInputAsync(writer, Encoding.UTF8.GetBytes(text), cancellationToken);
        }
    }
}