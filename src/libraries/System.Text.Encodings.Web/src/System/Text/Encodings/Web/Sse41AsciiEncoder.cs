// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using Statics = System.Text.Encodings.Web.Sse41AsciiEncoderStatics;

#pragma warning disable SA1121 // Use built-in type alias
using nuint = System.UInt32; // TODO: Replace me when real nuint type comes online

namespace System.Text.Encodings.Web
{
    internal unsafe struct State
    {
        public const int CharsMustEncodeLength = 128;

        public Vector128<byte> BitmapMask;
        public fixed bool CharsMustEncode[CharsMustEncodeLength];
    }

    internal static unsafe class AsciiEncoder
    {
        public static nuint FindIndexOfFirstByteToEncode(ref byte buffer, nuint bufferLength, in State state)
        {
            nuint curOffset = 0;

            //
            // First try 128-bit reads
            //

            if (bufferLength >= (uint)Vector128<byte>.Count)
            {
                nuint lastOffsetWhereCanRead = bufferLength - (uint)Vector128<byte>.Count;

            Loop:
                do
                {
                    Vector128<byte> vector = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.Add(ref buffer, (IntPtr)curOffset));

                    // For each element in 'vector', we use the low nibble as the mask for the shuffle.
                    // So given vector = [ 5A 20 39 7C ... ]
                    //  and bitmapMask = [ 00 11 22 33 ... ],
                    // we get shuffled = [ AA 00 99 CC ... ].
                    //
                    // Now we use the high nibble of each element of 'vector' to perform a bit check
                    // of each element within the shuffled vector. So if 'vector' has an element [ 5A ],
                    // this means "choose the element at index A from the bitmap, then determine whether
                    // the bit at position 5 is set within this element."
                    //
                    // PAND (lowNibble, highNibble) != [ 00 ] => bit 5 of element at index A in the bitmap *IS SET*
                    // PANDN(lowNibble, highNibble) != [ 00 ] => bit 5 of element at index A in the bitmap *IS NOT SET*
                    //
                    // We use "not set" to indicate that a particular value must be encoded. That is, if
                    // the incoming byte is 0x5A, then it must be encoded if bit 5 of the element at index
                    // A in the bitmap is not set.

                    Vector128<byte> lowNibbleShuffled = Ssse3.Shuffle(state.BitmapMask, vector);
                    Vector128<byte> highNibbleShuffled = Ssse3.Shuffle(Statics.PowersOfTwo, Sse2.And(Sse2.ShiftRightLogical(vector.AsInt16(), 4).AsByte(), Statics.LowNibble));

                    // Use the PTEST instruction as a short-circuit.

                    if (!Sse41.TestC(lowNibbleShuffled, highNibbleShuffled))
                    {
                        // We found a value that must be encoded. The bits of 'encodingMask'
                        // which are set correspond to the elements of 'vector' which must be
                        // encoded.

                        int encodingmask = Sse2.MoveMask(Sse2.CompareEqual(Sse2.And(lowNibbleShuffled, highNibbleShuffled), default));
                        Debug.Assert(encodingmask != 0);

                        return curOffset + (uint)BitOperations.TrailingZeroCount(encodingmask);
                    }

                    // All values can pass through unencoded.
                    curOffset += (uint)Vector128<byte>.Count;
                } while (curOffset <= lastOffsetWhereCanRead);

                if (lastOffsetWhereCanRead + (uint)Vector128<byte>.Count == curOffset)
                {
                    return curOffset; // processed the entire buffer
                }

                curOffset = lastOffsetWhereCanRead; // perform one final overlapping read from the end
                goto Loop;
            }

            //
            // Then try 64-bit reads
            //

            if (bufferLength >= (uint)Vector64<byte>.Count)
            {
            Loop:
                Vector128<byte> vector = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref buffer, (IntPtr)curOffset))).AsByte();

                // Same logic as above, but we can't use PTEST because the upper 8
                // bytes of the vector will have incorrect values.

                Vector128<byte> lowNibbleShuffled = Ssse3.Shuffle(state.BitmapMask, vector);
                Vector128<byte> highNibbleShuffled = Ssse3.Shuffle(Statics.PowersOfTwo, Sse2.And(Sse2.ShiftRightLogical(vector.AsInt16(), 4).AsByte(), Statics.LowNibble));

                int encodingmask = Sse2.MoveMask(Sse2.CompareEqual(Sse2.And(lowNibbleShuffled, highNibbleShuffled), default));
                if ((byte)encodingmask != 0)
                {
                    return curOffset + (uint)BitOperations.TrailingZeroCount(encodingmask);
                }

                // All values can pass through unencoded.
                curOffset += (uint)Vector64<byte>.Count;

                if (curOffset == bufferLength)
                {
                    return curOffset; // processed the entire buffer
                }

                curOffset = bufferLength - (uint)Vector64<byte>.Count; // perform one final overlapping read from the end
                goto Loop;
            }

            //
            // Small buffers (fewer than 8 elements)
            //

            for (; curOffset < bufferLength; curOffset++)
            {
                nuint el = Unsafe.Add(ref buffer, (IntPtr)(void*)curOffset);
                if (el >= State.CharsMustEncodeLength || state.CharsMustEncode[el])
                {
                    break;
                }
            }

            return curOffset;
        }

        public static nuint FindIndexOfFirstCharToEncode(ref char buffer, nuint bufferLength, in State state)
        {
            nuint curOffset = 0;

            //
            // First try 2x 128-bit reads
            //

            if (bufferLength >= (uint)Vector128<byte>.Count)
            {
                nuint lastOffsetWhereCanRead = bufferLength - (uint)Vector128<byte>.Count;

            Loop:
                do
                {
                    // We can get away with saturation in the below call because saturation will
                    // normalize out-of-range values to [ 00 ] or [ 80 .. FF ], which we already
                    // know are not present in the allow-list.

                    Vector128<byte> vector = Sse2.PackUnsignedSaturate(
                        Unsafe.ReadUnaligned<Vector128<short>>(ref Unsafe.As<char, byte>(ref buffer)),
                        Unsafe.ReadUnaligned<Vector128<short>>(ref Unsafe.Add(ref Unsafe.As<char, byte>(ref buffer), Vector128<byte>.Count)));

                    // From here, the logic is the same as the 'byte' case.

                    Vector128<byte> lowNibbleShuffled = Ssse3.Shuffle(state.BitmapMask, vector);
                    Vector128<byte> highNibbleShuffled = Ssse3.Shuffle(Statics.PowersOfTwo, Sse2.And(Sse2.ShiftRightLogical(vector.AsInt16(), 4).AsByte(), Statics.LowNibble));

                    // Use the PTEST instruction as a short-circuit.

                    if (!Sse41.TestC(lowNibbleShuffled, highNibbleShuffled))
                    {
                        // We found a value that must be encoded. The bits of 'encodingMask'
                        // which are set correspond to the elements of 'vector' which must be
                        // encoded.

                        int encodingmask = Sse2.MoveMask(Sse2.CompareEqual(Sse2.And(lowNibbleShuffled, highNibbleShuffled), default));
                        Debug.Assert(encodingmask != 0);

                        return curOffset + (uint)BitOperations.TrailingZeroCount(encodingmask);
                    }

                    // All values can pass through unencoded.
                    curOffset += (uint)Vector128<byte>.Count;
                } while (curOffset <= lastOffsetWhereCanRead);

                if (lastOffsetWhereCanRead + (uint)Vector128<byte>.Count == curOffset)
                {
                    return curOffset; // processed the entire buffer
                }

                curOffset = lastOffsetWhereCanRead; // perform one final overlapping read from the end
                goto Loop;
            }

            //
            // Then try 1x 128-bit reads
            //

            if (bufferLength >= (uint)Vector64<byte>.Count)
            {
            Loop:
                Vector128<byte> vector = Sse2.PackUnsignedSaturate(
                      Unsafe.ReadUnaligned<Vector128<short>>(ref Unsafe.As<char, byte>(ref buffer)),
                      default);

                // Same logic as above, but we can't use PTEST because the upper 8
                // bytes of the vector will have incorrect values.

                Vector128<byte> lowNibbleShuffled = Ssse3.Shuffle(state.BitmapMask, vector);
                Vector128<byte> highNibbleShuffled = Ssse3.Shuffle(Statics.PowersOfTwo, Sse2.And(Sse2.ShiftRightLogical(vector.AsInt16(), 4).AsByte(), Statics.LowNibble));

                int encodingmask = Sse2.MoveMask(Sse2.CompareEqual(Sse2.And(lowNibbleShuffled, highNibbleShuffled), default));
                if ((byte)encodingmask != 0)
                {
                    return curOffset + (uint)BitOperations.TrailingZeroCount(encodingmask);
                }

                // All values can pass through unencoded.
                curOffset += (uint)Vector64<byte>.Count;

                if (curOffset == bufferLength)
                {
                    return curOffset; // processed the entire buffer
                }

                curOffset = bufferLength - (uint)Vector64<byte>.Count; // perform one final overlapping read from the end
                goto Loop;
            }

            //
            // Small buffers (fewer than 8 elements)
            //

            for (; curOffset < bufferLength; curOffset++)
            {
                nuint el = Unsafe.Add(ref buffer, (IntPtr)(void*)curOffset);
                if (el >= State.CharsMustEncodeLength || state.CharsMustEncode[el])
                {
                    break;
                }
            }

            return curOffset;
        }
    }

    internal static class Sse41AsciiEncoderStatics
    {
        internal static readonly Vector128<byte> PowersOfTwo = Vector128.Create(
            0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);

        internal static readonly Vector128<byte> LowNibble = Vector128.Create((byte)0x0F);
    }

    internal unsafe readonly struct Sse41AsciiEncoder<TEncoder>
        where TEncoder : struct, IEncoderImplementation
    {
        private readonly Vector128<byte> _pshufbBitmask; // 0 = requires encoding, 1 = doesn't require encoding
        private readonly bool[] _asciiBytesWhichNeedEncoding;
        internal readonly TEncoder _encoder;

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

        public int FindIndexOfFirstCharToBeEncoded(ReadOnlySpan<char> value)
        {
            uint idx = FindIndexOfFirstCharToBeEncoded(ref MemoryMarshal.GetReference(value), (uint)value.Length);
            Debug.Assert(idx <= value.Length);

            return (idx < (uint)value.Length) ? (int)idx : -1;
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

        public bool WillEncode(uint value)
        {
            // The incoming value will encode if it's not ASCII or if it's
            // explicitly marked in the array as something that will encode.

            bool[] asciiBytesWhichNeedEncoding = _asciiBytesWhichNeedEncoding;
            return value >= (uint)asciiBytesWhichNeedEncoding.Length
                || asciiBytesWhichNeedEncoding[value];
        }

        public void Encode(ReadOnlySpan<char> value, TextWriter writer)
        {
            Debug.Assert(writer != null);

            int idx = FindIndexOfFirstCharToBeEncoded(value);
            if (idx == -1)
            {
                writer.Write(value);
                return;
            }

            writer.Write(value.Slice(0, idx));
            EncodeSlow(value.Slice(idx), writer);
        }

        private void EncodeSlow(ReadOnlySpan<char> input, TextWriter writer)
        {
            Debug.Assert(writer != null);

            Span<char> scratchBuffer = stackalloc char[_encoder.MaxOutputCharsPerInputRune];

            for (int i = 0; i < input.Length; i++)
            {
                char thisChar = input[i];
                if (!WillEncode(thisChar))
                {
                    writer.Write(thisChar); // no encoding needed
                    continue;
                }

                // Try extracting a scalar value from this single char.
                // Failing that, see if it's a surrogate pair.
                // If we still fail (due to a standalone surrogate char), use U+FFFD.

                if (!Rune.TryCreate(thisChar, out Rune rune))
                {
                    i++; // optimistically bump the index
                    if ((uint)i >= (uint)input.Length || !Rune.TryCreate(thisChar, input[i], out rune))
                    {
                        i--; // standalone surrogate char; undo bump
                        rune = Rune.ReplacementChar;
                    }
                }

                // Now encode the Rune and writes its contents to the writer

                int encodedCharsWritten = _encoder.EncodeToBuffer(rune, scratchBuffer);
                writer.Write(scratchBuffer.Slice(0, encodedCharsWritten));
            }
        }

        public string Encode(string value)
        {
            Debug.Assert(value != null);

            int idx = FindIndexOfFirstCharToBeEncoded(value);
            if (idx == -1)
            {
                return value; // unmodified
            }

            // worst case; let's say we're going to double the size of the input
            ValueStringBuilder builder = new ValueStringBuilder(value.Length * 2);

            builder.Append(value.AsSpan(0, idx));
            EncodeSlow(value.AsSpan(idx), ref builder);
            return builder.ToString(); // returns to pool
        }

        private void EncodeSlow(ReadOnlySpan<char> input, ref ValueStringBuilder builder)
        {
            for (int i = 0; i < input.Length; i++)
            {
                char thisChar = input[i];
                if (!WillEncode(thisChar))
                {
                    builder.Append(thisChar); // no encoding needed
                    continue;
                }

                // Try extracting a scalar value from this single char.
                // Failing that, see if it's a surrogate pair.
                // If we still fail (due to a standalone surrogate char), use U+FFFD.

                if (!Rune.TryCreate(thisChar, out Rune rune))
                {
                    i++; // optimistically bump the index
                    if ((uint)i >= (uint)input.Length || !Rune.TryCreate(thisChar, input[i], out rune))
                    {
                        i--; // standalone surrogate char; undo bump
                        rune = Rune.ReplacementChar;
                    }
                }

                // Now encode the Rune and writes its contents to the builder

                Span<char> scratchBuffer = builder.AppendSpan(_encoder.MaxOutputCharsPerInputRune);
                int encodedCharsWritten = _encoder.EncodeToBuffer(rune, scratchBuffer);
                builder.Length -= _encoder.MaxOutputCharsPerInputRune - encodedCharsWritten;
            }
        }
    }
}
