// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Internal.Runtime.CompilerServices;

namespace System.Buffers
{
    internal sealed unsafe partial class SecretSafeHandle : SafeHandle
    {
        // The real pointer.
        // We ignore the normal 'handle' field.
        private ShroudedPointer _shroudedPointer;

#pragma warning disable CA1419 // don't need public ctor since we're not using p/invoke shim to create this
        private SecretSafeHandle()
#pragma warning restore CA1419
            : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        public static SecretSafeHandle Allocate(nuint byteCount)
        {
            SecretSafeHandle retVal = new SecretSafeHandle();
            void* pAlloc = null;

            // We need to leave some room for our header data (byte length).
            // If this calculation would overflow, just normalize to OOM instead
            // of letting an OverflowException propagate its way up the stack.
            // Honestly this should never happen in practice.

            if (byteCount < (nuint)(nint)(-sizeof(IntPtr)))
            {
                nuint byteCountWithHeader = byteCount + (uint)sizeof(IntPtr);
                pAlloc = AllocateRaw(byteCountWithHeader);
            }

            if (pAlloc == null)
            {
                throw new OutOfMemoryException();
            }

            Unsafe.WriteUnaligned(pAlloc, byteCount); // header contains the number of raw bytes which follow
            retVal._shroudedPointer = new ShroudedPointer(pAlloc);
            return retVal;
        }

        // Caller must wrap this within a DangerousAddRef / DangerousRelease block.
        // If NativeSpan<byte> ever comes to fruition, out it instead.
        public void DangerousGetRawData(out nuint byteCount, out void* pData)
        {
            void* pHeader = _shroudedPointer.GetUnshroudedPointer().Value;
            byteCount = Unsafe.ReadUnaligned<nuint>(pHeader);
            pData = (byte*)pHeader + sizeof(nuint);
        }

        public SecretSafeHandle Duplicate()
        {
            bool originalSuccess = false;
            try
            {
                DangerousAddRef(ref originalSuccess);
                DangerousGetRawData(out nuint byteCount, out void* pSrc);
                SecretSafeHandle duplicateHandle = Allocate(byteCount);
                bool duplicateSuccess = false;
                try
                {
                    duplicateHandle.DangerousAddRef(ref duplicateSuccess);
                    DangerousGetRawData(out _, out void* pDest);
                    Buffer.Memmove(ref *(byte*)pDest, ref *(byte*)pSrc, byteCount);
                    return duplicateHandle;
                }
                finally
                {
                    if (duplicateSuccess)
                    {
                        duplicateHandle.DangerousRelease();
                    }
                }
            }
            finally
            {
                if (originalSuccess)
                {
                    DangerousRelease();
                }
            }
        }

        public override bool IsInvalid => _shroudedPointer.GetUnshroudedPointer().Value is not null;

        protected override bool ReleaseHandle()
        {
            void* pHeader = _shroudedPointer.GetUnshroudedPointer().Value;
            nuint byteCountExcludingHeader = Unsafe.ReadUnaligned<nuint>(pHeader);
            SpanHelpers.ClearWithoutReferences(ref *(byte*)pHeader, byteCountExcludingHeader + (nuint)sizeof(nuint));
            return ReleaseHandleImpl((IntPtr)pHeader);
        }
    }
}
