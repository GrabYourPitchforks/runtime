// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System
{
    internal static unsafe class RandomizedHashCodeValueTypeHelper
    {
        internal static int GetRandomizedHashCode<T>(in T bitwiseRepresentation)
            where T : unmanaged
            => Marvin.ComputeHash32(
                ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in bitwiseRepresentation)),
                (uint)sizeof(T),
                (uint)(RandomPerTypeSeed<T>.SeedValue >> 32),
                (uint)RandomPerTypeSeed<T>.SeedValue);

        internal static int GetRandomizedHashCodeChangeType<TExposed, TImplementation>(in TImplementation bitwiseRepresentation)
            where TExposed : unmanaged
            where TImplementation : unmanaged
        {
            Debug.Assert(sizeof(TExposed) == sizeof(TImplementation), "Unexpected size difference. Are you sure you're hashing all required data?");

            return Marvin.ComputeHash32(
                ref Unsafe.As<TImplementation, byte>(ref Unsafe.AsRef(in bitwiseRepresentation)),
                (uint)sizeof(TImplementation),
                (uint)(RandomPerTypeSeed<TExposed>.SeedValue >> 32),
                (uint)RandomPerTypeSeed<TExposed>.SeedValue);
        }

        private static class RandomPerTypeSeed<T> where T : unmanaged
        {
            internal static readonly ulong SeedValue = GetRandomPerTypeSeedValue();

            private static ulong GetRandomPerTypeSeedValue()
            {
                ulong value;
                Interop.GetRandomBytes((byte*)&value, sizeof(ulong));
                return value;
            }
        }
    }
}
