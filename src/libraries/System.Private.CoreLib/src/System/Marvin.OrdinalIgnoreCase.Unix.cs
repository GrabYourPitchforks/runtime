// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Text.Unicode;

namespace System
{
    internal static partial class Marvin
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint CaseFoldUInt32AsciiChars(uint data)
        {
            // On ICU, ordinal string comparisons are done by using simple case folding.
            // This process maps ASCII chars to their lowercase equivalents, so we'll
            // match that behavior here.

            return Utf16Utility.ConvertAllAsciiCharsInUInt32ToLowercase(data);
        }
    }
}
