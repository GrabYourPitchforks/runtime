// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Internal.Runtime.CompilerServices;

namespace System.Buffers
{
    internal unsafe sealed partial class SecretSafeHandle
    {
        private static readonly IntPtr _secretHeap = CreateSecretHeap();

        private static void AllocateImpl(nuint byteCount, out ShroudedPointer shroudedPointer)
        {
            void* pAlloc = null;

            // We need to leave some room for our header data (byte length).
            // If this calculation would overflow, just normalize to OOM instead
            // of letting an OverflowException propagate its way up the stack.
            // Honestly this should never happen in practice.

            if (byteCount < (nuint)(nint)(-sizeof(IntPtr)))
            {
                nuint realByteCount = byteCount + (uint)sizeof(IntPtr);
                pAlloc = (void*)HeapAlloc(_secretHeap, 0, realByteCount);
            }

            if (pAlloc == null)
            {
                throw new OutOfMemoryException();
            }

            Unsafe.WriteUnaligned(pAlloc, byteCount);
            shroudedPointer = new ShroudedPointer(pAlloc);
        }

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
