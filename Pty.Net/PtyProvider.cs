// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Pty.Net
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides the ability to spawn new processes under a pseudoterminal.
    /// </summary>
    public static class PtyProvider
    {
        private static readonly TraceSource Trace = new TraceSource(nameof(PtyProvider));

        /// <summary>
        /// Spawn a new process connected to a pseudoterminal.
        /// </summary>
        /// <param name="options">The set of options for creating the pseudoterminal.</param>
        /// <param name="cancellationToken">The token to cancel process creation early.</param>
        /// <returns>A <see cref="Task{IPtyConnection}"/> that completes once the process has spawned.</returns>
        public static Task<IPtyConnection> SpawnAsync(
            PtyOptions options,
            CancellationToken cancellationToken)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrEmpty(options.App))
            {
                throw new ArgumentException("A terminal app path must be specified.", nameof(options.App));
            }

            if (string.IsNullOrEmpty(options.Cwd))
            {
                throw new ArgumentException("A working directory must be specified.", nameof(options.Cwd));
            }

            if (options.CommandLine == null)
            {
                throw new ArgumentNullException(nameof(options.CommandLine));
            }

            if (options.Environment == null)
            {
                throw new ArgumentNullException(nameof(options.Environment));
            }

            // Merge the platform specific environment with the caller's environment.
            // A copy of the options is used so the caller's options object is never mutated.
            IDictionary<string, string> environment = MergeEnvironment(PlatformServices.PtyEnvironment, null);
            environment = MergeEnvironment(options.Environment, environment);

            PtyOptions mergedOptions = options.Copy();
            mergedOptions.Environment = environment;

            return PlatformServices.PtyProvider.StartTerminalAsync(mergedOptions, Trace, cancellationToken);
        }

        private static IDictionary<string, string> MergeEnvironment(IDictionary<string, string> environmentToMerge, IDictionary<string, string>? environment)
        {
            if (environment == null)
            {
                environment = new Dictionary<string, string>(PlatformServices.EnvironmentVariableComparer);
                foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                {
                    environment[entry.Key.ToString()] = entry.Value.ToString();
                }
            }

            foreach (var kvp in environmentToMerge)
            {
                if (string.IsNullOrEmpty(kvp.Value))
                {
                    environment.Remove(kvp.Key);
                }
                else
                {
                    environment[kvp.Key] = kvp.Value;
                }
            }

            return environment;
        }
    }
}
