// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System.Buffers
{
    // On non-Windows platforms, we use straight malloc / free to keep the secrets
    // off the managed heap.
    internal unsafe sealed partial class SecretSafeHandle
    {
        private static void* AllocateRaw(nuint byteCount) => NativeMemory.Alloc(byteCount);

        private static bool ReleaseHandleImpl(IntPtr unshroudedPointer)
        {
            NativeMemory.Free((void*)unshroudedPointer);
            return true; // success
        }
    }
}
