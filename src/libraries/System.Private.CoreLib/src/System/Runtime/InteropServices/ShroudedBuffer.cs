// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;

namespace System.Runtime.InteropServices
{
    /*
     * Represents data which should be "shrouded" from the rest of the application.
     * Shrouded data is sensitive data which should be difficult to *accidentally*
     * disclose, even accounting for some types of application bugs. For example,
     * we don't want accidental ArrayPool misuse to disclose the contents of shrouded
     * buffers. But there's no effort to thwart *intentional* disclosure of these
     * contents, such as through a debugger or memory dump utility.
     *
     * Some design philosophies and mitigations provided by this type:
     *
     * 1) The public surface area of this API works solely in Span<T>, not string
     *    or T[] or anything else. This gives the caller the ability to use pre-
     *    pinned memory if they so desire.
     *
     * 2) The virtual address space used to back the shrouded contents should be
     *    isolated from the virtual address space used by the rest of the
     *    framework or application code where practical. This helps avoid the
     *    case where the app has a dangling pointer or object reference that can
     *    be used to access the shrouded data.
     *
     * 3) The data is immutable once shrouded. If a caller wishes to mutate the
     *    contents, they must create a new instance. Immutability allows this
     *    object to be used by multiple callers simultaneously. (The Dispose
     *    method is not thread-safe.)
     *
     * 4) No reference is ever provided to the raw backing contents. This allows
     *    some future-proofing of the type, such as allowing the actual contents
     *    to be stored in a different process. (Think lsass / lsaiso.)
     *
     * *** THIS TYPE MAKES NO SECURITY GUARANTEES. ***
     *
     * This type is intended only to prevent accidental disclosure of the shrouded
     * contents. If protection against an active adversary is warranted, the
     * machine administrator should take additional steps such as: isolating the
     * process handling the sensitive data into its own service account, enabling
     * extra runtime safety checks, disabling memory dump collection, enabling OS-
     * wide hardware-backed safety mechanisms, and restricting access to the host.
     */
    public unsafe class ShroudedBuffer<T> : IDisposable where T : unmanaged
    {
        private readonly ShroudedBufferHandle _hnd;

        /// <summary>
        /// Creates a new <see cref="ShroudedBuffer{T}"/> from the provided contents.
        /// </summary>
        /// <param name="contents">The contents to copy into the new instance.</param>
        /// <remarks>
        /// The newly-returned <see cref="ShroudedBuffer{T}"/> instance maintains its
        /// own copy of the data separate from <paramref name="contents"/>.
        /// </remarks>
        public ShroudedBuffer(ReadOnlySpan<T> contents)
        {
            // multiplication below will never overflow
            _hnd = new ShroudedBufferHandle((nuint)Unsafe.SizeOf<T>() * (uint)contents.Length);
            Length = contents.Length;

            bool refAdded = false;
            try
            {
                _hnd.DangerousAddRef(ref refAdded);
                contents.CopyTo(new Span<T>((void*)_hnd.DangerousGetHandle(), Length));
            }
            finally
            {
                if (refAdded)
                {
                    _hnd.DangerousRelease();
                }
            }
        }

        /// <summary>
        /// Returns the length (in elements) of this buffer.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Copies the contents of this <see cref="ShroudedBuffer{T}"/> instance to
        /// a destination buffer.
        /// </summary>
        /// <param name="destination">
        /// The destination buffer which should receive the contents.
        /// This buffer must be at least <see cref="Length"/> elements in length.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="destination"/>'s length is smaller than <see cref="Length"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// This instance has already been disposed.
        /// </exception>
        public void CopyTo(Span<T> destination)
        {
            bool refAdded = false;
            try
            {
                _hnd.DangerousAddRef(ref refAdded);
                ReadOnlySpan<T> source = new ReadOnlySpan<T>((void*)_hnd.DangerousGetHandle(), Length);
                source.CopyTo(destination);
            }
            finally
            {
                if (refAdded)
                {
                    _hnd.DangerousRelease();
                }
            }
        }

        internal ShroudedBuffer<T> DeepClone()
        {
            bool refAdded = false;
            try
            {
                _hnd.DangerousAddRef(ref refAdded);
                return new ShroudedBuffer<T>(new ReadOnlySpan<T>((void*)_hnd.DangerousGetHandle(), Length));
            }
            finally
            {
                if (refAdded)
                {
                    _hnd.DangerousRelease();
                }
            }
        }

        /// <summary>
        /// Disposes of this instance, including any unmanaged resources.
        /// The contents will no longer be accessible once the instance is disposed.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of this instance, including any unmanaged resources.
        /// The contents will no longer be accessible once the instance is disposed.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> if this instance is being disposed through a call
        /// to the <see cref="Dispose"/> method; <see langword="false"/> if this instance
        /// is being finalized.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            _hnd.Dispose();
        }
    }

    internal sealed partial class ShroudedBufferHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private readonly nuint _cbData; // used to clear memory before freeing

        // allocates memory; does not guarantee zero-init
        internal ShroudedBufferHandle(nuint cbData)
             : base(ownsHandle: true)
        {
            _cbData = cbData;
            SetHandle(AllocateCore());

            if (IsInvalid)
            {
                throw new OutOfMemoryException();
            }
        }

        protected override unsafe bool ReleaseHandle()
        {
            // zero the memory before releasing it
            SpanHelpers.ClearWithoutReferences(ref Unsafe.AsRef<byte>((void*)handle), _cbData);
            return ReleaseHandleCore();
        }
    }
}
