// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if NETCOREAPP3_1_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif

#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics.Arm;
#endif

#if SYSTEM_PRIVATE_CORELIB
using Internal.Runtime.CompilerServices;
#endif

// Some routines inspired by the Stanford Bit Twiddling Hacks by Sean Eron Anderson:
// http://graphics.stanford.edu/~seander/bithacks.html

namespace System.Numerics
{
    /// <summary>
    /// Utility methods for intrinsic bit-twiddling operations.
    /// The methods use hardware intrinsics when available on the underlying platform,
    /// otherwise they use optimized software fallbacks.
    /// </summary>
#if SYSTEM_PRIVATE_CORELIB
    public
#else
    internal
#endif
        static partial class BitOperations
    {
        // C# no-alloc optimization that directly wraps the data section of the dll (similar to string constants)
        // https://github.com/dotnet/roslyn/pull/24621

        private static ReadOnlySpan<byte> TrailingZeroCountDeBruijn => new byte[32]
        {
            00, 01, 28, 02, 29, 14, 24, 03,
            30, 22, 20, 15, 25, 17, 04, 08,
            31, 27, 13, 23, 21, 19, 16, 07,
            26, 12, 18, 06, 11, 05, 10, 09
        };

        /// <summary>
        /// Count the number of trailing zero bits in an integer value.
        /// Similar in behavior to the x86 instruction TZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(int value)
            => TrailingZeroCount((uint)value);

        /// <summary>
        /// Count the number of trailing zero bits in an integer value.
        /// Similar in behavior to the x86 instruction TZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(uint value)
        {
#if NETCOREAPP3_1_OR_GREATER
            if (Bmi1.IsSupported)
            {
                // TZCNT contract is 0->32
                return (int)Bmi1.TrailingZeroCount(value);
            }
#endif

#if NET5_0_OR_GREATER
            if (ArmBase.IsSupported)
            {
                return ArmBase.LeadingZeroCount(ArmBase.ReverseElementBits(value));
            }
#endif

            // Unguarded fallback contract is 0->0, BSF contract is 0->undefined
            if (value == 0)
            {
                return 32;
            }

#if NET5_0_OR_GREATER
            if (X86Base.IsSupported)
            {
                return (int)X86Base.BitScanForward(value);
            }
#endif

            // uint.MaxValue >> 27 is always in range [0 - 31] so we use Unsafe.AddByteOffset to avoid bounds check
            return Unsafe.AddByteOffset(
                // Using deBruijn sequence, k=2, n=5 (2^5=32) : 0b_0000_0111_0111_1100_1011_0101_0011_0001u
                ref MemoryMarshal.GetReference(TrailingZeroCountDeBruijn),
                // uint|long -> IntPtr cast on 32-bit platforms does expensive overflow checks not needed here
                (IntPtr)(int)(((value & (uint)-(int)value) * 0x077CB531u) >> 27)); // Multi-cast mitigates redundant conv.u8
        }

        /// <summary>
        /// Count the number of trailing zero bits in a mask.
        /// Similar in behavior to the x86 instruction TZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(long value)
            => TrailingZeroCount((ulong)value);

        /// <summary>
        /// Count the number of trailing zero bits in a mask.
        /// Similar in behavior to the x86 instruction TZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static int TrailingZeroCount(ulong value)
        {
#if NETCOREAPP3_1_OR_GREATER
            if (Bmi1.X64.IsSupported)
            {
                // TZCNT contract is 0->64
                return (int)Bmi1.X64.TrailingZeroCount(value);
            }
#endif

#if NET5_0_OR_GREATER
            if (ArmBase.Arm64.IsSupported)
            {
                return ArmBase.Arm64.LeadingZeroCount(ArmBase.Arm64.ReverseElementBits(value));
            }

            if (X86Base.X64.IsSupported)
            {
                // BSF contract is 0->undefined
                return value == 0 ? 64 : (int)X86Base.X64.BitScanForward(value);
            }
#endif

            uint lo = (uint)value;

            if (lo == 0)
            {
                return 32 + TrailingZeroCount((uint)(value >> 32));
            }

            return TrailingZeroCount(lo);
        }
    }
}
