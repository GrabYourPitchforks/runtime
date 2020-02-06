// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Globalization
{
    public partial class TextInfo
    {
        private unsafe void FinishInitialization()
        {
            _sortHandle = CompareInfo.GetSortHandle(_textInfoName);
        }

        // For internal use only. Performs case folding of data from the source buffer
        // to the destination buffer. The conversion is ordinal / non-linguistic.
        internal static void CaseFold(ReadOnlySpan<char> source, Span<char> destination)
        {
            Debug.Assert(destination.Length >= source.Length);

            if (GlobalizationMode.Invariant)
            {
                ToUpperAsciiInvariant(source, destination);
            }
            else
            {
                CaseFoldImpl(source, destination);
            }
        }

        private static unsafe void CaseFoldImpl(ReadOnlySpan<char> source, Span<char> destination)
        {
            Debug.Assert(!GlobalizationMode.Invariant);

            // Windows (NLS) doesn't have an implementation of simple case folding.
            // Instead, the NLS code paths normalize to uppercase using the invariant culture.

            fixed (char* pSource = &MemoryMarshal.GetReference(source))
            fixed (char* pDestination = &MemoryMarshal.GetReference(destination))
            {
                Invariant.ChangeCase(pSource, source.Length, pDestination, destination.Length, toUpper: true);
            }
        }

        private unsafe void ChangeCase(char* pSource, int pSourceLen, char* pResult, int pResultLen, bool toUpper)
        {
            Debug.Assert(!GlobalizationMode.Invariant);
            Debug.Assert(pSource != null);
            Debug.Assert(pResult != null);
            Debug.Assert(pSourceLen >= 0);
            Debug.Assert(pResultLen >= 0);
            Debug.Assert(pSourceLen <= pResultLen);

            // Check for Invariant to avoid A/V in LCMapStringEx
            uint linguisticCasing = IsInvariantLocale(_textInfoName) ? 0 : LCMAP_LINGUISTIC_CASING;

            int ret = Interop.Kernel32.LCMapStringEx(_sortHandle != IntPtr.Zero ? null : _textInfoName,
                                                     linguisticCasing | (toUpper ? LCMAP_UPPERCASE : LCMAP_LOWERCASE),
                                                     pSource,
                                                     pSourceLen,
                                                     pResult,
                                                     pSourceLen,
                                                     null,
                                                     null,
                                                     _sortHandle);
            if (ret == 0)
            {
                throw new InvalidOperationException(SR.InvalidOperation_ReadOnly);
            }

            Debug.Assert(ret == pSourceLen, "Expected getting the same length of the original string");
        }

        // PAL Ends here

        private IntPtr _sortHandle;

        private const uint LCMAP_LINGUISTIC_CASING = 0x01000000;
        private const uint LCMAP_LOWERCASE = 0x00000100;
        private const uint LCMAP_UPPERCASE = 0x00000200;

        private static bool IsInvariantLocale(string localeName) => localeName == "";
    }
}
