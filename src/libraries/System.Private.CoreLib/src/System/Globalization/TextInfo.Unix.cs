// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace System.Globalization
{
    public partial class TextInfo
    {
        private Tristate _needsTurkishCasing = Tristate.NotInitialized;

        private void FinishInitialization() { }

        // -----------------------------
        // ---- PAL layer ends here ----
        // -----------------------------

        private static bool NeedsTurkishCasing(string localeName)
        {
            Debug.Assert(localeName != null);

            return CultureInfo.GetCultureInfo(localeName).CompareInfo.Compare("\u0131", "I", CompareOptions.IgnoreCase) == 0;
        }

        private bool IsInvariant { get { return _cultureName.Length == 0; } }

        // For internal use only. Performs case folding of data from the source buffer
        // to the destination buffer. The conversion is ordinal / non-linguistic.
        internal static void CaseFold(ReadOnlySpan<char> source, Span<char> destination)
        {
            Debug.Assert(destination.Length >= source.Length);

            if (GlobalizationMode.Invariant)
            {
                ToLowerAsciiInvariant(source, destination);
            }
            else
            {
                CaseFoldImpl(source, destination);
            }
        }

        private static unsafe void CaseFoldImpl(ReadOnlySpan<char> source, Span<char> destination)
        {
            Debug.Assert(!GlobalizationMode.Invariant);
            Debug.Assert(destination.Length >= source.Length);

            if (destination.Length < source.Length)
            {
                ThrowHelper.ThrowArgumentException_DestinationTooShort();
            }

            fixed (char* pSource = &MemoryMarshal.GetReference(source))
            fixed (char* pDestination = &MemoryMarshal.GetReference(destination))
            {
                Interop.Globalization.CaseFold(pSource, pDestination, source.Length);
            }
        }

        internal unsafe void ChangeCase(char* src, int srcLen, char* dstBuffer, int dstBufferCapacity, bool bToUpper)
        {
            Debug.Assert(!GlobalizationMode.Invariant);

            if (IsInvariant)
            {
                Interop.Globalization.ChangeCaseInvariant(src, srcLen, dstBuffer, dstBufferCapacity, bToUpper);
            }
            else
            {
                if (_needsTurkishCasing == Tristate.NotInitialized)
                {
                    _needsTurkishCasing = NeedsTurkishCasing(_textInfoName) ? Tristate.True : Tristate.False;
                }
                if (_needsTurkishCasing == Tristate.True)
                {
                    Interop.Globalization.ChangeCaseTurkish(src, srcLen, dstBuffer, dstBufferCapacity, bToUpper);
                }
                else
                {
                    Interop.Globalization.ChangeCase(src, srcLen, dstBuffer, dstBufferCapacity, bToUpper);
                }
            }
        }

    }
}
