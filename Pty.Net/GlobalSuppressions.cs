// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.
//
// Native interop types are fully documented (SA1600/SA1602 are not suppressed),
// but keep the naming-rule exemptions: native function/field names are lowercase
// or contain underscores by definition and cannot follow .NET naming conventions.
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Native interop classes are excluded from naming convention rules.", Scope = "type", Target = "Pty.Net.Windows.WinptyNativeInterop")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Native interop classes are excluded from naming convention rules.", Scope = "type", Target = "Pty.Net.Windows.WinptyNativeInterop")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Native interop classes are excluded from naming convention rules.", Scope = "type", Target = "Pty.Net.Windows.WinptyNativeInterop")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Native interop classes are excluded from naming convention rules.", Scope = "type", Target = "Pty.Net.Linux.NativeMethods")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Native interop classes are excluded from naming convention rules.", Scope = "type", Target = "Pty.Net.Linux.NativeMethods")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Native interop classes are excluded from naming convention rules.", Scope = "type", Target = "Pty.Net.Linux.NativeMethods")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Native interop classes are excluded from naming convention rules.", Scope = "type", Target = "Pty.Net.Mac.NativeMethods")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Native interop classes are excluded from naming convention rules.", Scope = "type", Target = "Pty.Net.Mac.NativeMethods")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Native interop classes are excluded from naming convention rules.", Scope = "type", Target = "Pty.Net.Mac.NativeMethods")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1011:Closing square brackets should be spaced correctly", Justification = "false positive due to nullable type annotation", Scope = "type", Target = "Pty.Net.Windows.WindowsArguments")]
