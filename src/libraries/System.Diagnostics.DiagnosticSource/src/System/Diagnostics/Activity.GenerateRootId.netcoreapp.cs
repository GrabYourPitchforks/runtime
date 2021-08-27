// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;

namespace System.Diagnostics
{
    partial class Activity
    {
        private static string GenerateRootId()
        {
            // It is important that the part that changes frequently be first, because
            // some sampling functions don't sample from the high entropy part of their hash function.
            // This makes sampling based on this produce poor samples.
            Span<char> result = stackalloc char[1 + 2 * sizeof(long)]; // assume s_currentRootId is long
            result[0] = '|';
            bool formatted = Interlocked.Increment(ref s_currentRootId).TryFormat(result.Slice(1), out int charsWritten, "x");
            Debug.Assert(formatted);
            Debug.Assert(charsWritten == 2 * sizeof(long)); // validate s_currentRootId is long
            return string.Concat(result, s_uniqSuffix);
        }
    }
}
