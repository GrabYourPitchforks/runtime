// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Unicode;

namespace System
{
    internal static partial class Marvin
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint CaseFoldUInt32AsciiChars(uint data)
        {
            // On Windows (NLS), the CompareStringOrdinal function works by taking each
            // scalar value and mapping it to its culture-agnostic uppercase equivalent.
            // We'll map ASCII chars to uppercase to match that behavior.

            return Utf16Utility.ConvertAllAsciiCharsInUInt32ToUppercase(data);
        }
    }
}
