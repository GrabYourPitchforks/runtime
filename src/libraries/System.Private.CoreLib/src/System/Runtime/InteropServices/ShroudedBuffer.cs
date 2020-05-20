// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;

namespace System.Runtime.InteropServices
{
    public unsafe class ShroudedBuffer<T> : IDisposable where T : unmanaged
    {
        private readonly SafeLocalAllocHandle _hnd;

        public ShroudedBuffer(ReadOnlySpan<T> contents)
        {
            // multiplication below will never overflow
            _hnd = SafeLocalAllocHandle.LocalAlloc((nuint)Unsafe.SizeOf<T>() * (uint)contents.Length);
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

        public int Length { get; }

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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            _hnd.Dispose();
        }
    }
}
