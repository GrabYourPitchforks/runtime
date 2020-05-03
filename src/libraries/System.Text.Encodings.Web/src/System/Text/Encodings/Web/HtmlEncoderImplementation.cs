// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace System.Text.Encodings.Web
{
    internal readonly struct HtmlEncoderImplementation : IEncoderImplementation
    {
        public int MaxOutputCharsPerInputRune => 10; // = "&#x10FFFF;".Length

        public int EncodeToBuffer(Rune rune, Span<char> buffer)
        {
            if ((uint)MaxOutputCharsPerInputRune - 1 >= (uint)buffer.Length)
            {
                Debug.Fail("Caller passed a bad scratch buffer.");
                buffer[-1] = default; // will throw IndexOutOfBoundsException
                return default;
            }

            int i = 0;
            buffer[i++] = '&';

            if (rune.Value == '\"')
            {
                buffer[i++] = 'q';
                buffer[i++] = 'u';
                buffer[i++] = 'o';
                buffer[i++] = 't';
                buffer[i++] = ';';
                return i;
            }

            if (rune.Value == '<')
            {
                buffer[i++] = 'l';
                buffer[i++] = 't';
                buffer[i++] = ';';
                return i;
            }

            if (rune.Value == '>')
            {
                buffer[i++] = 'g';
                buffer[i++] = 't';
                buffer[i++] = ';';
                return i;
            }

            if (rune.Value == '&')
            {
                buffer[i++] = 'a';
                buffer[i++] = 'm';
                buffer[i++] = 'p';
                buffer[i++] = ';';
                return i;
            }

            // If we reached this point, we don't have an entity mapping.
            // We need to write the rune as a hex-encoded value.
            // We always write at least one nibble (for '0').

            buffer[1] = '#';
            buffer[2] = 'x';

            uint value = (uint)rune.Value;
            buffer = buffer.Slice(3, (BitOperations.Log2(value) / 4) + 2);

            // This is essentially a reverse 'for' loop, but it's written to take
            // advantage of JIT bounds check elision.

            i = buffer.Length - 2;
            while (true)
            {
                if ((uint)i >= (uint)buffer.Length)
                {
                    break;
                }

                char upperHex = HexConverter.ToCharUpper((int)value);
                buffer[i--] = upperHex;
                value >>= 4;
            }

            buffer[buffer.Length - 1] = ';'; // unfortunately can't elide bounds check
            return buffer.Length + 3; // account for the "&#x" written previously
        }

        public bool TryEncodeToBuffer(Rune rune, Span<char> buffer, out int charsWritten)
        {
            int i = 0;

            if (rune.Value == '\"')
            {
                if (5 >= (uint)buffer.Length) { goto BufferTooSmall; }
                buffer[i++] = '&';
                buffer[i++] = 'q';
                buffer[i++] = 'u';
                buffer[i++] = 'o';
                buffer[i++] = 't';
                buffer[i++] = ';';
                goto Success;
            }

            if (rune.Value == '<')
            {
                if (3 >= (uint)buffer.Length) { goto BufferTooSmall; }
                buffer[i++] = '&';
                buffer[i++] = 'l';
                buffer[i++] = 't';
                buffer[i++] = ';';
                goto Success;
            }

            if (rune.Value == '>')
            {
                if (3 >= (uint)buffer.Length) { goto BufferTooSmall; }
                buffer[i++] = '&';
                buffer[i++] = 'g';
                buffer[i++] = 't';
                buffer[i++] = ';';
                goto Success;
            }

            if (rune.Value == '&')
            {
                if (4 >= (uint)buffer.Length) { goto BufferTooSmall; }
                buffer[i++] = '&';
                buffer[i++] = 'a';
                buffer[i++] = 'm';
                buffer[i++] = 'p';
                buffer[i++] = ';';
                goto Success;
            }

            // If we reached this point, we don't have an entity mapping.
            // We need to write the rune as a hex-encoded value.
            // We always write at least one nibble (for '0').
            // Require at least 5 chars ("&#x0;") min buffer size.

            if (4 >= (uint)buffer.Length) { goto BufferTooSmall; }

            buffer[0] = '&';
            buffer[1] = '#';
            buffer[2] = 'x';

            uint value = (uint)rune.Value;
            int actualDigitCount = BitOperations.Log2(value) / 4;
            for (i = actualDigitCount; i >= 0; i--)
            {
                if ((uint)(3 + i) >= (uint)buffer.Length) { goto BufferTooSmall; }
                char upperHex = HexConverter.ToCharUpper((int)value);
                buffer[3 + i] = upperHex;
                value >>= 4;
            }

            i = actualDigitCount + 4;
            if ((uint)i >= (uint)buffer.Length) { goto BufferTooSmall; }
            buffer[i] = ';';

        Success:
            charsWritten = i;
            return true;

        BufferTooSmall:
            charsWritten = default;
            return false;
        }
    }
}
