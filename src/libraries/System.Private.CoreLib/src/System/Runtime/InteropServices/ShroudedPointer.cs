// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    // Represents a pointer whose value is shrouded. By 'shrouded', we go to extra lengths
    // to ensure the value is not accessible via typical public or private reflection patterns.
    // This mainly covers the ShroudedPointer instance being passed to a serializer or logger,
    // either of which might walk the object and try to dump its information.
    //
    // Our protections:
    // - There is no field or property which contains the shrouded value.
    //   Reduces risk of "iterate all fields / properties" logic accessing the value.
    // - The unshrouded value is returned via a helper ref struct, which cannot be boxed.
    //   Reduces risk of "call arbitrary method" logic accessing the value.
    // - Value exposed as raw pointer instead of IntPtr.
    //   Reduces risk of auto-conversion from pointer value to equivalent numeric type
    //   exposing the value.
    //
    // This type may optionally use other mechanisms to further protect the data, such as
    // RtlEncodePointer or CryptProtectMemory, but this isn't necessary unless we have
    // reason to believe that the data contained within this struct might be inadvertently
    // exposed through mechanisms not covered by the above protections. It is an explicit
    // NON-GOAL to avoid disclosure of the pointer value from somebody who can execute
    // code within the process or who can dump memory.
    //
    // The ifdef below is specially crafted such that if this type is ever copied to a
    // project which does not define any TARGET_*BIT value, we'll fall back to 64-bit size.
#if TARGET_32BIT
    [StructLayout(LayoutKind.Explicit, Size = 4)]
#else
    [StructLayout(LayoutKind.Explicit, Size = 8)]
#endif
    internal unsafe readonly struct ShroudedPointer
    {
        internal ShroudedPointer(void* value)
        {
            IntPtr valueIntPtr = (IntPtr)value;
            MemoryMarshal.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref this, 1)), ref valueIntPtr);
        }

        internal Result GetUnshroudedPointer()
        {
            ref ShroudedPointer mutableThis = ref Unsafe.AsRef(in this); // we promise not to mutate
            return new Result((void*)MemoryMarshal.Read<IntPtr>(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref mutableThis, 1))));
        }

        internal readonly ref struct Result
        {
            internal readonly void* Value;

            internal Result(void* value)
            {
                Value = value;
            }
        }
    }
}
