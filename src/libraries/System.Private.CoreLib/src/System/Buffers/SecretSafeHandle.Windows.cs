// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System.Buffers
{
    // On Windows, we create a standalone private heap used for storing our raw secret data.
    // This private heap is never destroyed for the lifetime of the process. This is a
    // defense-in-depth measure to further minimize the chance of a dangling malloc pointer
    // referencing the raw data.
    internal unsafe sealed partial class SecretSafeHandle
    {
        private static readonly IntPtr _secretHeap = CreateSecretHeap();

        private static void* AllocateRaw(nuint byteCount) => (void*)HeapAlloc(_secretHeap, 0, byteCount);

        private static IntPtr CreateSecretHeap()
        {
            IntPtr hHeap = HeapCreate(0, 0, 0);
            if (hHeap == IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            return hHeap;
        }

        private static bool ReleaseHandleImpl(IntPtr unshroudedPointer)
            => HeapFree(_secretHeap, 0, unshroudedPointer) != Interop.BOOL.FALSE;

        // https://docs.microsoft.com/windows/win32/api/heapapi/nf-heapapi-heapalloc
        [DllImport(Interop.Libraries.Kernel32, CharSet = CharSet.Unicode)]
        private static extern IntPtr HeapAlloc(IntPtr hHeap, int dwFlags, nuint dwBytes);

        // https://docs.microsoft.com/windows/win32/api/heapapi/nf-heapapi-heapcreate
        [DllImport(Interop.Libraries.Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr HeapCreate(int flOptions, nuint dwInitialSize, nuint dwMaximumSize);

        // https://docs.microsoft.com/windows/win32/api/heapapi/nf-heapapi-heapfree
        [DllImport(Interop.Libraries.Kernel32, CharSet = CharSet.Unicode)]
        private static extern Interop.BOOL HeapFree(IntPtr Heap, int dwFlags, IntPtr lpMem);
    }
}
