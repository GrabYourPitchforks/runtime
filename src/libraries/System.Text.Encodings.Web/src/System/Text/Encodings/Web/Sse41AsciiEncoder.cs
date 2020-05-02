// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using Statics = System.Text.Encodings.Web.Sse41AsciiEncoderStatics;

namespace System.Text.Encodings.Web
{
    internal static class Sse41AsciiEncoderStatics
    {
        internal static readonly Vector128<byte> PowersOfTwo = Vector128.Create(
            0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);

        internal static readonly Vector128<byte> LowNibble = Vector128.Create((byte)0x0F);
    }

    internal unsafe readonly struct Sse41AsciiEncoder<TEncoder> where TEncoder : IEncoderImplementation
    {
        private readonly Vector128<byte> _pshufbBitmask; // 0 = requires encoding, 1 = doesn't require encoding
        private readonly bool[] _asciiBytesWhichNeedEncoding;
        private readonly TEncoder _encoder;

        internal Sse41AsciiEncoder(TEncoder encoder)
        {
            Debug.Assert(Sse41.IsSupported);

            _pshufbBitmask = default;
            _asciiBytesWhichNeedEncoding = default!;
            _encoder = encoder;
        }

        public int FindIndexOfFirstByteToBeEncoded(ReadOnlySpan<byte> buffer)
        {
            int encodingMask;
            ref byte startOfBuffer = ref MemoryMarshal.GetReference(buffer);

            while (MemoryMarshal.TryRead(buffer, out Vector128<byte> vector))
            {
                Vector128<byte> lowNibbleShuffled = Sse41.Shuffle(_pshufbBitmask, vector);
                Vector128<byte> highNibbleShuffled = Sse41.Shuffle(Statics.PowersOfTwo, Sse41.And(Sse41.ShiftRightLogical(vector.AsInt16(), 4).AsByte(), Statics.LowNibble));

                if (Sse41.TestC(lowNibbleShuffled, highNibbleShuffled))
                {
                    buffer = buffer.Slice(Vector128<byte>.Count);
                    continue;
                }

                // Found data that needs to be encoded.
                // Figure out its index in the buffer and return.

                encodingMask = Sse41.MoveMask(Sse41.CompareEqual(default(Vector128<byte>), Sse41.And(lowNibbleShuffled, highNibbleShuffled)));
                goto MaskContainsDataWhichRequiresEncoding;
            }

            if (MemoryMarshal.TryRead(buffer, out Vector64<byte> vector64))
            {
                Vector128<byte> vector = vector64.ToVector128Unsafe();
                Vector128<byte> lowNibbleShuffled = Sse41.Shuffle(_pshufbBitmask, vector);
                Vector128<byte> highNibbleShuffled = Sse41.Shuffle(Statics.PowersOfTwo, Sse41.And(Sse41.ShiftRightLogical(vector.AsInt16(), 4).AsByte(), Statics.LowNibble));

                // Can't use same PTEST trick as earlier since the upper
                // 8 elements of the vector contain uninitialized data.

                encodingMask = Sse41.MoveMask(Sse41.CompareEqual(default(Vector128<byte>), Sse41.And(lowNibbleShuffled, highNibbleShuffled)));
                encodingMask &= 0xFFFF; // we only care about the low 16 bits

                if (encodingMask != 0)
                {
                    goto MaskContainsDataWhichRequiresEncoding;
                }

                buffer = buffer.Slice(Vector64<byte>.Count);
            }

            Debug.Assert(buffer.Length < 8);

            bool[] bytesWhichNeedEncoding = _asciiBytesWhichNeedEncoding;
            int i;
            for (i = 0; i < buffer.Length; i++)
            {
                int value = buffer[i];
                if ((uint)value >= (uint)bytesWhichNeedEncoding.Length || bytesWhichNeedEncoding[value])
                {
                    break;
                }
            }

            return (int)(void*)Unsafe.ByteOffset(ref startOfBuffer, ref MemoryMarshal.GetReference(buffer)) + i;

        MaskContainsDataWhichRequiresEncoding:

            Debug.Assert(encodingMask != 0);
            return (int)(void*)Unsafe.ByteOffset(ref startOfBuffer, ref MemoryMarshal.GetReference(buffer))
                   + BitOperations.TrailingZeroCount(encodingMask);
        }

        public uint FindIndexOfFirstCharToBeEncoded(ref char buffer, uint bufferLength)
        {
            int encodingMask;
            ref char startOfBuffer = ref buffer;

            while (bufferLength >= 16)
            {
                // We can get away with saturation in the below call because
                // saturation will normalize out-of-range values to [ 00 ]
                // or [ 80 .. FF ], which we already know are not present
                // in the allow-list.

                Vector128<byte> vector = Sse41.PackUnsignedSaturate(
                    Unsafe.ReadUnaligned<Vector128<short>>(ref Unsafe.As<char, byte>(ref buffer)),
                    Unsafe.ReadUnaligned<Vector128<short>>(ref Unsafe.Add(ref Unsafe.As<char, byte>(ref buffer), 16)));

                Vector128<byte> lowNibbleShuffled = Sse41.Shuffle(_pshufbBitmask, vector);
                Vector128<byte> highNibbleShuffled = Sse41.Shuffle(Statics.PowersOfTwo, Sse41.And(Sse41.ShiftRightLogical(vector.AsInt16(), 4).AsByte(), Statics.LowNibble));

                if (Sse41.TestC(lowNibbleShuffled, highNibbleShuffled))
                {
                    buffer = ref Unsafe.Add(ref buffer, 16);
                    bufferLength -= 16;
                    continue;
                }

                // Found data that needs to be encoded.
                // Figure out its index in the buffer and return.

                encodingMask = Sse41.MoveMask(Sse41.CompareEqual(default(Vector128<byte>), Sse41.And(lowNibbleShuffled, highNibbleShuffled)));
                goto MaskContainsDataWhichRequiresEncoding;
            }

            if (bufferLength >= 8)
            {
                // See earlier comment re: why we can use saturation here.

                Vector128<byte> vector = Sse41.PackUnsignedSaturate(
                   Unsafe.ReadUnaligned<Vector64<short>>(ref Unsafe.As<char, byte>(ref buffer)).ToVector128Unsafe(),
                   Unsafe.ReadUnaligned<Vector64<short>>(ref Unsafe.Add(ref Unsafe.As<char, byte>(ref buffer), 8)).ToVector128Unsafe());

                Vector128<byte> lowNibbleShuffled = Sse41.Shuffle(_pshufbBitmask, vector);
                Vector128<byte> highNibbleShuffled = Sse41.Shuffle(Statics.PowersOfTwo, Sse41.And(Sse41.ShiftRightLogical(vector.AsInt16(), 4).AsByte(), Statics.LowNibble));

                // Can't use same PTEST trick as earlier since the upper
                // 8 elements of the vector contain uninitialized data.

                encodingMask = Sse41.MoveMask(Sse41.CompareEqual(default(Vector128<byte>), Sse41.And(lowNibbleShuffled, highNibbleShuffled)));
                encodingMask &= 0xFFFF; // we only care about the low 16 bits

                if (encodingMask != 0)
                {
                    goto MaskContainsDataWhichRequiresEncoding;
                }

                buffer = ref Unsafe.Add(ref buffer, 8);
                bufferLength -= 8;
            }

            Debug.Assert(bufferLength < 8);

            bool[] bytesWhichNeedEncoding = _asciiBytesWhichNeedEncoding;
            uint i;
            for (i = 0; i < bufferLength; i++)
            {
                int value = Unsafe.Add(ref buffer, (IntPtr)(void*)i); // TODO: use nuint when available
                if ((uint)value >= (uint)bytesWhichNeedEncoding.Length || bytesWhichNeedEncoding[value])
                {
                    break;
                }
            }

            return ((uint)(void*)Unsafe.ByteOffset(ref startOfBuffer, ref buffer) + i) / sizeof(char);

        MaskContainsDataWhichRequiresEncoding:

            Debug.Assert(encodingMask != 0);
            return ((uint)(void*)Unsafe.ByteOffset(ref startOfBuffer, ref buffer)
                   + (uint)BitOperations.TrailingZeroCount(encodingMask)) / sizeof(char);
        }
    }
}
