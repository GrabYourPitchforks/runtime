// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

using System.Numerics;
using Internal.Runtime.CompilerServices;

namespace System
{
    internal static partial class SpanHelpers
    {
        public static unsafe void ClearWithoutReferences(ref byte b, nuint byteLength)
        {
            if (byteLength == 0)
                return;

#if TARGET_AMD64 || TARGET_ARM64
            // The exact matrix on when ZeroMemory is faster than InitBlockUnaligned is very complex. The factors to consider include
            // type of hardware and memory alignment. This threshold was chosen as a good balance across different configurations.
            if (byteLength > 768)
                goto PInvoke;
            Unsafe.InitBlockUnaligned(ref b, 0, (uint)byteLength);
            return;
#else
            // TODO: Optimize other platforms to be on par with AMD64 CoreCLR
            // Note: It's important that this switch handles lengths at least up to 22.
            // See notes below near the main loop for why.

            // The switch will be very fast since it can be implemented using a jump
            // table in assembly. See http://stackoverflow.com/a/449297/4077294 for more info.

            switch (byteLength)
            {
                case 1:
                    b = 0;
                    return;
                case 2:
                    Unsafe.As<byte, short>(ref b) = 0;
                    return;
                case 3:
                    Unsafe.As<byte, short>(ref b) = 0;
                    Unsafe.Add<byte>(ref b, 2) = 0;
                    return;
                case 4:
                    Unsafe.As<byte, int>(ref b) = 0;
                    return;
                case 5:
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.Add<byte>(ref b, 4) = 0;
                    return;
                case 6:
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, short>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
                    return;
                case 7:
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, short>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
                    Unsafe.Add<byte>(ref b, 6) = 0;
                    return;
                case 8:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
#endif
                    return;
                case 9:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
#endif
                    Unsafe.Add<byte>(ref b, 8) = 0;
                    return;
                case 10:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
#endif
                    Unsafe.As<byte, short>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    return;
                case 11:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
#endif
                    Unsafe.As<byte, short>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    Unsafe.Add<byte>(ref b, 10) = 0;
                    return;
                case 12:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
#endif
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    return;
                case 13:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
#endif
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    Unsafe.Add<byte>(ref b, 12) = 0;
                    return;
                case 14:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
#endif
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    Unsafe.As<byte, short>(ref Unsafe.Add<byte>(ref b, 12)) = 0;
                    return;
                case 15:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
#endif
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    Unsafe.As<byte, short>(ref Unsafe.Add<byte>(ref b, 12)) = 0;
                    Unsafe.Add<byte>(ref b, 14) = 0;
                    return;
                case 16:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
                    Unsafe.As<byte, long>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 12)) = 0;
#endif
                    return;
                case 17:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
                    Unsafe.As<byte, long>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 12)) = 0;
#endif
                    Unsafe.Add<byte>(ref b, 16) = 0;
                    return;
                case 18:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
                    Unsafe.As<byte, long>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 12)) = 0;
#endif
                    Unsafe.As<byte, short>(ref Unsafe.Add<byte>(ref b, 16)) = 0;
                    return;
                case 19:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
                    Unsafe.As<byte, long>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 12)) = 0;
#endif
                    Unsafe.As<byte, short>(ref Unsafe.Add<byte>(ref b, 16)) = 0;
                    Unsafe.Add<byte>(ref b, 18) = 0;
                    return;
                case 20:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
                    Unsafe.As<byte, long>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 12)) = 0;
#endif
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 16)) = 0;
                    return;
                case 21:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
                    Unsafe.As<byte, long>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 12)) = 0;
#endif
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 16)) = 0;
                    Unsafe.Add<byte>(ref b, 20) = 0;
                    return;
                case 22:
#if TARGET_64BIT
                    Unsafe.As<byte, long>(ref b) = 0;
                    Unsafe.As<byte, long>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
#else
                    Unsafe.As<byte, int>(ref b) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 4)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 8)) = 0;
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 12)) = 0;
#endif
                    Unsafe.As<byte, int>(ref Unsafe.Add<byte>(ref b, 16)) = 0;
                    Unsafe.As<byte, short>(ref Unsafe.Add<byte>(ref b, 20)) = 0;
                    return;
            }

            // P/Invoke into the native version for large lengths
            if (byteLength >= 512) goto PInvoke;

            nuint i = 0; // byte offset at which we're copying

            if (((nuint)Unsafe.AsPointer(ref b) & 3) != 0)
            {
                if (((nuint)Unsafe.AsPointer(ref b) & 1) != 0)
                {
                    b = 0;
                    i += 1;
                    if (((nuint)Unsafe.AsPointer(ref b) & 2) != 0)
                        goto IntAligned;
                }
                Unsafe.As<byte, short>(ref Unsafe.AddByteOffset<byte>(ref b, i)) = 0;
                i += 2;
            }

            IntAligned:

            // On 64-bit IntPtr.Size == 8, so we want to advance to the next 8-aligned address. If
            // (int)b % 8 is 0, 5, 6, or 7, we will already have advanced by 0, 3, 2, or 1
            // bytes to the next aligned address (respectively), so do nothing. On the other hand,
            // if it is 1, 2, 3, or 4 we will want to copy-and-advance another 4 bytes until
            // we're aligned.
            // The thing 1, 2, 3, and 4 have in common that the others don't is that if you
            // subtract one from them, their 3rd lsb will not be set. Hence, the below check.

            if ((((nuint)Unsafe.AsPointer(ref b) - 1) & 4) == 0)
            {
                Unsafe.As<byte, int>(ref Unsafe.AddByteOffset<byte>(ref b, i)) = 0;
                i += 4;
            }

            nuint end = byteLength - 16;
            byteLength -= i; // lower 4 bits of byteLength represent how many bytes are left *after* the unrolled loop

            // We know due to the above switch-case that this loop will always run 1 iteration; max
            // bytes we clear before checking is 23 (7 to align the pointers, 16 for 1 iteration) so
            // the switch handles lengths 0-22.
            Debug.Assert(end >= 7 && i <= end);

            // This is separated out into a different variable, so the i + 16 addition can be
            // performed at the start of the pipeline and the loop condition does not have
            // a dependency on the writes.
            nuint counter;

            do
            {
                counter = i + 16;

                // This loop looks very costly since there appear to be a bunch of temporary values
                // being created with the adds, but the jit (for x86 anyways) will convert each of
                // these to use memory addressing operands.

                // So the only cost is a bit of code size, which is made up for by the fact that
                // we save on writes to b.

#if TARGET_64BIT
                Unsafe.As<byte, long>(ref Unsafe.AddByteOffset<byte>(ref b, i)) = 0;
                Unsafe.As<byte, long>(ref Unsafe.AddByteOffset<byte>(ref b, i + 8)) = 0;
#else
                Unsafe.As<byte, int>(ref Unsafe.AddByteOffset<byte>(ref b, i)) = 0;
                Unsafe.As<byte, int>(ref Unsafe.AddByteOffset<byte>(ref b, i + 4)) = 0;
                Unsafe.As<byte, int>(ref Unsafe.AddByteOffset<byte>(ref b, i + 8)) = 0;
                Unsafe.As<byte, int>(ref Unsafe.AddByteOffset<byte>(ref b, i + 12)) = 0;
#endif

                i = counter;

                // See notes above for why this wasn't used instead
                // i += 16;
            }
            while (counter <= end);

            if ((byteLength & 8) != 0)
            {
#if TARGET_64BIT
                Unsafe.As<byte, long>(ref Unsafe.AddByteOffset<byte>(ref b, i)) = 0;
#else
                Unsafe.As<byte, int>(ref Unsafe.AddByteOffset<byte>(ref b, i)) = 0;
                Unsafe.As<byte, int>(ref Unsafe.AddByteOffset<byte>(ref b, i + 4)) = 0;
#endif
                i += 8;
            }
            if ((byteLength & 4) != 0)
            {
                Unsafe.As<byte, int>(ref Unsafe.AddByteOffset<byte>(ref b, i)) = 0;
                i += 4;
            }
            if ((byteLength & 2) != 0)
            {
                Unsafe.As<byte, short>(ref Unsafe.AddByteOffset<byte>(ref b, i)) = 0;
                i += 2;
            }
            if ((byteLength & 1) != 0)
            {
                Unsafe.AddByteOffset<byte>(ref b, i) = 0;
                // We're not using i after this, so not needed
                // i += 1;
            }

            return;
#endif

        PInvoke:
            Buffer._ZeroMemory(ref b, byteLength);
        }

        public static unsafe void ClearWithReferences(ref IntPtr ip, nuint pointerSizeLength)
        {
            Debug.Assert((nint)Unsafe.AsPointer(ref ip) % sizeof(IntPtr) == 0, "Should've been aligned on natural word boundary.");

#if CORECLR // Mono's JIT or target architectures may mishandle the vectorized optimization
            if (Vector.IsHardwareAccelerated && pointerSizeLength >= 8)
            {
                // We're relying on the JIT to turn the writes below into SIMD stores.
                // Currently the JIT prefers 128-bit stores, so we'll help it along by ensuring
                // the main write loop is 128-bit aligned. Technically the GC could kick in and
                // throw off the alignment mid-loop, but this should be rare; and if for whatever
                // reason it does happen, the only conseqeunce will be that we'll see a slowdown
                // due to unaligned writes. It's not the end of the world.

                // TODO: Actually implement alignment nonsense.


                // We have enough data for at least one bulk (vectorized) write.

                nuint currentByteOffset = 0;
                nuint byteOffsetWhereCanPerformFinalLoopIteration = (pointerSizeLength - 8) * (nuint)IntPtr.Size;

                // Write 8 refs, which is 256 - 512 bits, which is 1 - 4 SIMD vectors.

                do
                {
                    Unsafe.AddByteOffset(ref Unsafe.As<IntPtr, EightRefs>(ref ip), currentByteOffset) = default;
                } while ((currentByteOffset += (nuint)Unsafe.SizeOf<EightRefs>()) <= byteOffsetWhereCanPerformFinalLoopIteration);

                // Write 4 refs, which is 128 - 256 bits, which is 1 - 2 SIMD vectors.

                if ((pointerSizeLength & 4) != 0)
                {
                    Unsafe.AddByteOffset(ref Unsafe.As<IntPtr, FourRefs>(ref ip), currentByteOffset) = default;
                    currentByteOffset += (nuint)Unsafe.SizeOf<FourRefs>();
                }

                // Write 2 refs, which is 64 - 128 bits, which is 1 SIMD vector or 2 scalars.

                if ((pointerSizeLength & 2) != 0)
                {
                    Unsafe.AddByteOffset(ref Unsafe.As<IntPtr, TwoRefs>(ref ip), currentByteOffset) = default;
                }

                // Unconditionally write the last element as scalar
                Unsafe.Add(ref Unsafe.As<IntPtr, object?>(ref ip), (nint)pointerSizeLength - 1) = default;
            }
            else
#endif
            {
                // If we reached this point, vectorization is disabled, or there are too few
                // elements for us to vectorize. Fall back to an unrolled loop.

                nuint i = 0;

                // Write 8 elements at a time

#if CORECLR
                if (!Vector.IsHardwareAccelerated) // If vectorization enabled, 8-element case would've been handled earlier, no need to check again
#endif
                {
                    if (pointerSizeLength >= 8)
                    {
                        nuint stopLoopAtOffset = pointerSizeLength & ~(nuint)7;
                        do
                        {
                            Unsafe.Add(ref ip, (nint)i + 0) = default;
                            Unsafe.Add(ref ip, (nint)i + 1) = default;
                            Unsafe.Add(ref ip, (nint)i + 2) = default;
                            Unsafe.Add(ref ip, (nint)i + 3) = default;
                            Unsafe.Add(ref ip, (nint)i + 4) = default;
                            Unsafe.Add(ref ip, (nint)i + 5) = default;
                            Unsafe.Add(ref ip, (nint)i + 6) = default;
                            Unsafe.Add(ref ip, (nint)i + 7) = default;
                        } while ((i += 8) < stopLoopAtOffset);
                    }
                }

                // Write next 4 elements if needed

                if ((pointerSizeLength & 4) != 0)
                {
                    Unsafe.Add(ref ip, (nint)i + 0) = default;
                    Unsafe.Add(ref ip, (nint)i + 1) = default;
                    Unsafe.Add(ref ip, (nint)i + 2) = default;
                    Unsafe.Add(ref ip, (nint)i + 3) = default;
                    i += 4;
                }

                // Write next 2 elements if needed

                if ((pointerSizeLength & 2) != 0)
                {
                    Unsafe.Add(ref ip, (nint)i + 0) = default;
                    Unsafe.Add(ref ip, (nint)i + 1) = default;
                    i += 2;
                }

                // Write final element if needed

                if ((pointerSizeLength & 1) != 0)
                {
                    Unsafe.Add(ref ip, (nint)i) = default;
                }
            }
        }

#if CORECLR
#pragma warning disable CA1823, IDE0051, CS0169 // Avoid unused private fields
        private readonly struct EightRefs
        {
            private readonly object? _data0;
            private readonly object? _data1;
            private readonly object? _data2;
            private readonly object? _data3;
            private readonly object? _data4;
            private readonly object? _data5;
            private readonly object? _data6;
            private readonly object? _data7;
        }

        private readonly struct FourRefs
        {
            private readonly object? _data0;
            private readonly object? _data1;
            private readonly object? _data2;
            private readonly object? _data3;
        }

        private readonly struct TwoRefs
        {
            private readonly object? _data0;
            private readonly object? _data1;
        }
#pragma warning restore CA1823, IDE0051, CS0169 // Avoid unused private fields
#endif
    }
}
