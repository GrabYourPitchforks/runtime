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
            if ((uint)MaxOutputCharsPerInputRune >= (uint)buffer.Length)
            {
                Debug.Fail("Caller passed a bad scratch buffer.");
                goto Fail; // this should never happen
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
            int nibblesToWrite = BitOperations.Log2(value) / 4;
            for (i = 0; i <= nibblesToWrite; i++)
            {
                char upperHex = HexConverter.ToCharUpper((int)value);
                buffer[3 + i] = upperHex;
                value >>= 4;
            }

            buffer[4 + nibblesToWrite] = ';';
            return 5 + nibblesToWrite;

        Fail:
            buffer[-1] = '\0'; // will throw IndexOutOfBoundsException
            return 0;
        }
    }
}
