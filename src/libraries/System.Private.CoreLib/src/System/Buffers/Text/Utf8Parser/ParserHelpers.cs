// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace System.Buffers.Text
{
    internal static class ParserHelpers
    {
        public const int ByteOverflowLength = 3;
        public const int ByteOverflowLengthHex = 2;
        public const int UInt16OverflowLength = 5;
        public const int UInt16OverflowLengthHex = 4;
        public const int UInt32OverflowLength = 10;
        public const int UInt32OverflowLengthHex = 8;
        public const int UInt64OverflowLength = 20;
        public const int UInt64OverflowLengthHex = 16;

        public const int SByteOverflowLength = 3;
        public const int SByteOverflowLengthHex = 2;
        public const int Int16OverflowLength = 5;
        public const int Int16OverflowLengthHex = 4;
        public const int Int32OverflowLength = 10;
        public const int Int32OverflowLengthHex = 8;
        public const int Int64OverflowLength = 19;
        public const int Int64OverflowLengthHex = 16;

        public static ReadOnlySpan<byte> HexLookup => new byte[] // rely on C# compiler optimization to reference static data
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 15
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 31
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 47
            0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,                       // 63
            0xFF, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,                   // 79
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 95
            0xFF, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,                   // 111
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 127
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 143
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 159
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 175
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 191
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 207
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 223
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,             // 239
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF              // 255
        };

        public static ReadOnlySpan<sbyte> HexLookupSByte => new sbyte[] // rely on C# compiler optimization to reference static data
        {
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 15
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 31
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 47
            unchecked((sbyte)0x00), unchecked((sbyte)0x01), unchecked((sbyte)0x02), unchecked((sbyte)0x03), unchecked((sbyte)0x04), unchecked((sbyte)0x05), unchecked((sbyte)0x06), unchecked((sbyte)0x07), unchecked((sbyte)0x08), unchecked((sbyte)0x09), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),                       // 63
            unchecked((sbyte)0xFF), unchecked((sbyte)0x0A), unchecked((sbyte)0x0B), unchecked((sbyte)0x0C), unchecked((sbyte)0x0D), unchecked((sbyte)0x0E), unchecked((sbyte)0x0F), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),                   // 79
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 95
            unchecked((sbyte)0xFF), unchecked((sbyte)0x0a), unchecked((sbyte)0x0b), unchecked((sbyte)0x0c), unchecked((sbyte)0x0d), unchecked((sbyte)0x0e), unchecked((sbyte)0x0f), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),                   // 111
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 127
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 143
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 159
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 175
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 191
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 207
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 223
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF),             // 239
            unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF), unchecked((sbyte)0xFF)              // 255
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDigit(int i)
        {
            return (uint)(i - '0') <= ('9' - '0');
        }

        //
        // Enable use of ThrowHelper from TryParse() routines without introducing dozens of non-code-coveraged "value= default; bytesConsumed = 0; return false" boilerplate.
        //
        public static bool TryParseThrowFormatException(out int bytesConsumed)
        {
            bytesConsumed = 0;
            ThrowHelper.ThrowFormatException_BadFormatSpecifier();
            return false;
        }

        //
        // Enable use of ThrowHelper from TryParse() routines without introducing dozens of non-code-coveraged "value= default; bytesConsumed = 0; return false" boilerplate.
        //
        public static bool TryParseThrowFormatException<T>(out T value, out int bytesConsumed) where T : struct
        {
            value = default;
            return TryParseThrowFormatException(out bytesConsumed);
        }
    }
}
