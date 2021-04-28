// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace System.Text
{
    internal static partial class ASCIIUtility
    {
        /// <summary>
        /// A mask which selects only the high bit of each byte of the given <see cref="uint"/>.
        /// </summary>
        private const uint UInt32HighBitsOnlyMask = 0x80808080u;

        /// <summary>
        /// A mask which selects only the high bit of each byte of the given <see cref="ulong"/>.
        /// </summary>
        private const ulong UInt64HighBitsOnlyMask = 0x80808080_80808080ul;

        /// <summary>
        /// Returns <see langword="true"/> iff all bytes in <paramref name="value"/> are ASCII.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool AllBytesInUInt32AreAscii(uint value)
        {
            // If the high bit of any byte is set, that byte is non-ASCII.

            return (value & UInt32HighBitsOnlyMask) == 0;
        }

        /// <summary>
        /// Given a UInt32 that represents four ASCII bytes, returns the invariant lowercase
        /// representation of those characters. Requires the input value to contain four ASCII
        /// bytes. Input and output are in machine endianness.
        /// </summary>
        /// <remarks>
        /// This is a branchless implementation.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ConvertAllAsciiBytesInUInt32ToLowercase(uint value)
        {
            // Keep this in sync with Utf16Utility.ConvertAllAsciiCharsInUInt32ToLowercase.
            // ASSUMPTION: Caller has validated that input value is ASCII.
            Debug.Assert(AllBytesInUInt32AreAscii(value));

            // the 0x80 bit of each byte of 'lowerIndicator' will be set iff the byte has value >= 'A'
            uint lowerIndicator = value + 0x8080_8080u - 0x4141_4141u;

            // the 0x80 bit of each byte of 'upperIndicator' will be set iff the byte has value > 'Z'
            uint upperIndicator = value + 0x8080_8080u - 0x5B5B_5B5Bu;

            // the 0x80 bit of each byte of 'combinedIndicator' will be set iff the byte has value >= 'A' and <= 'Z'
            uint combinedIndicator = (lowerIndicator ^ upperIndicator);

            // the 0x20 bit of each byte of 'mask' will be set iff the byte has value >= 'A' and <= 'Z'
            uint mask = (combinedIndicator & 0x8080_8080u) >> 2;

            return value ^ mask; // bit flip uppercase letters [A-Z] => [a-z]
        }

        /// <summary>
        /// Given a UInt32 that represents four ASCII bytes, returns the invariant uppercase
        /// representation of those characters. Requires the input value to contain four ASCII
        /// bytes. Input and output are in machine endianness.
        /// </summary>
        /// <remarks>
        /// This is a branchless implementation.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ConvertAllAsciiBytesInUInt32ToUppercase(uint value)
        {
            // Keep this in sync with Utf16Utility.ConvertAllAsciiCharsInUInt32ToUppercase.
            // ASSUMPTION: Caller has validated that input value is ASCII.
            Debug.Assert(AllBytesInUInt32AreAscii(value));

            // the 0x80 bit of each byte of 'lowerIndicator' will be set iff the byte has value >= 'a'
            uint lowerIndicator = value + 0x8080_8080u - 0x6161_6161u;

            // the 0x80 bit of each byte of 'upperIndicator' will be set iff the byte has value > 'z'
            uint upperIndicator = value + 0x8080_8080u - 0x7B7B_7B7Bu;

            // the 0x80 bit of each byte of 'combinedIndicator' will be set iff the byte has value >= 'a' and <= 'z'
            uint combinedIndicator = (lowerIndicator ^ upperIndicator);

            // the 0x20 bit of each byte of 'mask' will be set iff the byte has value >= 'a' and <= 'z'
            uint mask = (combinedIndicator & 0x8080_8080u) >> 2;

            return value ^ mask; // bit flip lowercase letters [a-z] => [A-Z]
        }

        /// <summary>
        /// Given a DWORD which represents a four-byte buffer read in machine endianness, and which
        /// the caller has asserted contains a non-ASCII byte *somewhere* in the data, counts the
        /// number of consecutive ASCII bytes starting from the beginning of the buffer. Returns
        /// a value 0 - 3, inclusive. (The caller is responsible for ensuring that the buffer doesn't
        /// contain all-ASCII data.)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint CountNumberOfLeadingAsciiBytesFromUInt32WithSomeNonAsciiData(uint value)
        {
            Debug.Assert(!AllBytesInUInt32AreAscii(value), "Caller shouldn't provide an all-ASCII value.");

            if (BitConverter.IsLittleEndian)
            {
                return (uint)BitOperations.TrailingZeroCount(value & UInt32HighBitsOnlyMask) >> 3;
            }
            else
            {
                // Couldn't use tzcnt, use specialized software fallback.
                // The 'allBytesUpToNowAreAscii' DWORD uses bit twiddling to hold a 1 or a 0 depending
                // on whether all processed bytes were ASCII. Then we accumulate all of the
                // results to calculate how many consecutive ASCII bytes are present.

                value = ~value;

                // BinaryPrimitives.ReverseEndianness is only implemented as an intrinsic on
                // little-endian platforms, so using it in this big-endian path would be too
                // expensive. Instead we'll just change how we perform the shifts.

                // Read first byte
                value = BitOperations.RotateLeft(value, 1);
                uint allBytesUpToNowAreAscii = value & 1;
                uint numAsciiBytes = allBytesUpToNowAreAscii;

                // Read second byte
                value = BitOperations.RotateLeft(value, 8);
                allBytesUpToNowAreAscii &= value;
                numAsciiBytes += allBytesUpToNowAreAscii;

                // Read third byte
                value = BitOperations.RotateLeft(value, 8);
                allBytesUpToNowAreAscii &= value;
                numAsciiBytes += allBytesUpToNowAreAscii;

                return numAsciiBytes;
            }
        }
    }
}
