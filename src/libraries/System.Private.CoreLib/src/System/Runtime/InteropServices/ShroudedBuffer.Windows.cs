// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.InteropServices
{
    internal sealed partial class ShroudedBufferHandle
    {
        private static readonly IntPtr _hHeap = GetOrCreateHeap();

        internal IntPtr AllocateCore()
            => Interop.HeapAlloc(_hHeap, 0, _cbData);

        private static IntPtr GetOrCreateHeap()
        {
            // In 64-bit processes where the virtual address space is larger,
            // we use our own private heap to store the shrouded data. This
            // somewhat isolates the shrouded data's storage space from that of
            // the rest of the application, providing limited mitigation of a
            // use-after-free elsewhere in the application accidentally pointing
            // to an address used by a shrouded buffer instance.

            IntPtr hHeap = (IntPtr.Size == 8) ? Interop.HeapCreate(0, 0, 0) : Interop.GetProcessHeap();
            if (hHeap == IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                Environment.FailFast("Couldn't get heap information.");
            }

            return hHeap;
        }

        private bool ReleaseHandleCore()
        {
            return Interop.HeapFree(_hHeap, 0, handle);
        }

        private static class Interop
        {
            private const string KERNEL32_LIB = "kernel32.dll";

            [DllImport(KERNEL32_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
            internal static extern IntPtr GetProcessHeap();

            [DllImport(KERNEL32_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
            internal static extern IntPtr HeapCreate(uint flOptions, nuint dwInitialSize, nuint dwMaximumSize);

            [DllImport(KERNEL32_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = false)]
            internal static extern IntPtr HeapAlloc(IntPtr hHeap, uint dwFlags, nuint dwBytes);

            [DllImport(KERNEL32_LIB, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
            internal static extern bool HeapFree(IntPtr hHeap, uint dwFlags, IntPtr lpMem);
        }
    }
}
