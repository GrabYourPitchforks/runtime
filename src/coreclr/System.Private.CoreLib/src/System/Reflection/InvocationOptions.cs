// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Runtime.InteropServices;

namespace System.Reflection
{
    /// <summary>
    /// Used for passing infrequently used arguments through the managed reflection stack.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct InvocationOptions
    {
        internal Binder? Binder { get; init; }
        internal BindingFlags BindingFlags { get; init; }
        internal CultureInfo? Culture { get; init; }
    }
}
