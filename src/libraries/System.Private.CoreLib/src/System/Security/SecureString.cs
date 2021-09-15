// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;

namespace System.Security
{
    public unsafe sealed partial class SecureString : IDisposable
    {
        private const int MaxLength = 65536;
        private const int MaxStackAlloc = 128; // measured in chars; we realistically expect very small values

        private readonly object _methodLock = new object();
        private bool _readOnly;
        private Secret<char>? _secret;

        public SecureString()
        {
            _secret = new Secret<char>(ReadOnlySpan<char>.Empty);
        }

        [CLSCompliant(false)]
        public unsafe SecureString(char* value, int length)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (length > MaxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_Length);
            }

            _secret = new Secret<char>(new ReadOnlySpan<char>(value, length));
        }

        private SecureString(SecureString str)
        {
            Debug.Assert(str._secret is not null, "Expected other SecureString's buffer to be non-null");
            _secret = str._secret.Clone();
        }

        public int Length
        {
            get
            {
                lock (_methodLock)
                {
                    EnsureNotDisposed();
                    return _secret.GetLength();
                }
            }
        }

        private static char[] AllocateArray(int capacity)
        {
            if (capacity > MaxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), SR.ArgumentOutOfRange_Capacity);
            }

            return new char[capacity]; // we expect these to be small, so keep them in Gen0 instead of using the pinned heap
        }

        public void AppendChar(char c)
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();
                EnsureNotReadOnly();

                int newCapacity = checked(_secret.GetLength() + 1);
                Span<char> scratchBuffer = ((uint)newCapacity <= MaxStackAlloc) ? stackalloc char[MaxStackAlloc] : AllocateArray(newCapacity); // sensitive data; don't use ArrayPool
                fixed (char* pUnused = &MemoryMarshal.GetReference(scratchBuffer)) // prevent array from being moved in memory
                {
                    try
                    {
                        int index = _secret.RevealInto(scratchBuffer);
                        scratchBuffer[index] = c; // append
                        scratchBuffer = scratchBuffer.Slice(0, index + 1); // because buffer may be oversized
                        SwapSecretUnderLock(scratchBuffer);
                    }
                    finally
                    {
                        scratchBuffer.Clear();
                    }
                }
            }
        }

        // clears the current contents. Only available if writable
        public void Clear()
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();
                EnsureNotReadOnly();

                _secret = new Secret<char>(ReadOnlySpan<char>.Empty);
            }
        }

        // Do a deep-copy of the SecureString
        public SecureString Copy()
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();
                return new SecureString(this);
            }
        }

        public void Dispose()
        {
            lock (_methodLock)
            {
                _secret?.Dispose();
                _secret = null;
            }
        }

        public void InsertAt(int index, char c)
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();
                EnsureNotReadOnly();

                if ((uint)index >= (uint)Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_IndexString);
                }

                _secret.RevealAndUse((@this: this, index, c), static (span, state) =>
                {
                    int newCapacity = checked(state.@this._secret!.GetLength() + 1);
                    Span<char> scratchBuffer = ((uint)newCapacity <= MaxStackAlloc) ? stackalloc char[MaxStackAlloc] : AllocateArray(newCapacity); // sensitive data; don't use ArrayPool
                    fixed (char* pUnused = &MemoryMarshal.GetReference(scratchBuffer)) // prevent array from being moved in memory
                    {
                        try
                        {
                            span.Slice(0, state.index).CopyTo(scratchBuffer);
                            scratchBuffer[state.index] = state.c;
                            span.Slice(state.index).CopyTo(scratchBuffer.Slice(state.index + 1));
                            scratchBuffer = scratchBuffer.Slice(span.Length + 1); // because buffer may be oversized
                            state.@this.SwapSecretUnderLock(scratchBuffer);
                        }
                        finally
                        {
                            scratchBuffer.Clear();
                        }
                    }
                });
            }
        }

        public bool IsReadOnly()
        {
            EnsureNotDisposed();
            return Volatile.Read(ref _readOnly);
        }

        public void MakeReadOnly()
        {
            EnsureNotDisposed();
            Volatile.Write(ref _readOnly, true);
        }

        public void RemoveAt(int index)
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();
                EnsureNotReadOnly();

                if ((uint)index >= (uint)Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_IndexString);
                }

                _secret.RevealAndUse((@this: this, index), static (span, state) =>
                {
                    int newCapacity = checked(state.@this._secret!.GetLength() - 1);
                    Span<char> scratchBuffer = ((uint)newCapacity <= MaxStackAlloc) ? stackalloc char[MaxStackAlloc] : AllocateArray(newCapacity); // sensitive data; don't use ArrayPool
                    fixed (char* pUnused = &MemoryMarshal.GetReference(scratchBuffer)) // prevent array from being moved in memory
                    {
                        try
                        {
                            span.Slice(0, state.index).CopyTo(scratchBuffer);
                            span.Slice(state.index + 1).CopyTo(scratchBuffer.Slice(state.index));
                            scratchBuffer = scratchBuffer.Slice(span.Length - 1); // because buffer may be oversized
                            state.@this.SwapSecretUnderLock(scratchBuffer);
                        }
                        finally
                        {
                            scratchBuffer.Clear();
                        }
                    }
                });
            }
        }

        public void SetAt(int index, char c)
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();
                EnsureNotReadOnly();

                if ((uint)index >= (uint)Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_IndexString);
                }

                int newCapacity = _secret.GetLength();
                Span<char> scratchBuffer = ((uint)newCapacity <= MaxStackAlloc) ? stackalloc char[MaxStackAlloc] : AllocateArray(newCapacity); // sensitive data; don't use ArrayPool
                fixed (char* pUnused = &MemoryMarshal.GetReference(scratchBuffer)) // prevent array from being moved in memory
                {
                    try
                    {
                        _secret.RevealInto(scratchBuffer);
                        scratchBuffer[index] = c;
                        scratchBuffer = scratchBuffer.Slice(newCapacity); // because buffer may be oversized
                        SwapSecretUnderLock(scratchBuffer);
                    }
                    finally
                    {
                        scratchBuffer.Clear();
                    }
                }
            }
        }

        // This method should be called under lock.
        [MemberNotNull(nameof(_secret))]
        private void EnsureNotDisposed()
        {
            Debug.Assert(Monitor.IsEntered(_methodLock), "Caller didn't take the lock before invoking this method.");
            if (_secret is null)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }


        // This method should be called under lock.
        private void EnsureNotReadOnly()
        {
            Debug.Assert(Monitor.IsEntered(_methodLock), "Caller didn't take the lock before invoking this method.");

            if (_readOnly)
            {
                throw new InvalidOperationException(SR.InvalidOperation_ReadOnly);
            }
        }

        // Replaces the secret with a new object.
        // This method should be called under lock.
        private void SwapSecretUnderLock(ReadOnlySpan<char> newSecret)
        {
            Debug.Assert(Monitor.IsEntered(_methodLock), "Caller didn't take the lock before invoking this method.");

            Secret<char> oldSecret = _secret!;
            _secret = new Secret<char>(newSecret);
            oldSecret.Dispose();
        }

        internal IntPtr MarshalToBSTR()
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();

                IntPtr ptr = IntPtr.Zero;
                _secret.RevealAndUse((IntPtr)(&ptr), static (span, addrOfPtr) =>
                {
                    IntPtr ptr = Marshal.AllocBSTR(span.Length); // will throw if allocation fails
                    span.CopyTo(new Span<char>((void*)ptr, span.Length)); // will not fail
                    *(IntPtr*)addrOfPtr = ptr;
                });

                Debug.Assert(ptr != IntPtr.Zero); // allocation should have succeeded
                return ptr;
            }
        }

        internal unsafe IntPtr MarshalToString(bool globalAlloc, bool unicode)
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();

                delegate* managed<int, IntPtr> pfnAllocator = (globalAlloc) ? &Marshal.AllocHGlobal : &Marshal.AllocCoTaskMem;
                delegate* managed<IntPtr, void> pfnDeallocator = (globalAlloc) ? &Marshal.FreeHGlobal : &Marshal.FreeCoTaskMem;
                return (unicode) ? MarshalToUnicodeString(pfnAllocator) : MarshalToAnsiString(pfnAllocator, pfnDeallocator);
            }

            IntPtr MarshalToAnsiString(delegate* managed<int, IntPtr> pfnAllocator, delegate* managed<IntPtr, void> pfnDeallocator)
            {
                IntPtr ptr = IntPtr.Zero;
                _secret!.RevealAndUse((addrOfPtr: (IntPtr)(&ptr), pfnAllocator: (IntPtr)pfnAllocator, pfnDeallocator: (IntPtr)pfnDeallocator), static (span, state) =>
                {
                    int byteCount = Marshal.GetAnsiStringByteCount(span); // throws on failure
                    IntPtr ptr = ((delegate* managed<int, IntPtr>)state.pfnAllocator)(byteCount); // throws on OOM
                    bool success = false;

                    try
                    {
                        Marshal.GetAnsiStringBytes(span, new Span<byte>((void*)ptr, byteCount)); // could fail
                        success = true;
                    }
                    finally
                    {
                        if (!success)
                        {
                            Debug.Assert(ptr != IntPtr.Zero); // we know the allocator succeeded
                            ((delegate* managed<IntPtr, void>)state.pfnDeallocator)(ptr);
                        }
                    }

                    *(IntPtr*)state.addrOfPtr = ptr;
                });
                Debug.Assert(ptr != IntPtr.Zero);
                return ptr;
            }

            IntPtr MarshalToUnicodeString(delegate* managed<int, IntPtr> pfnAllocator)
            {
                IntPtr ptr = IntPtr.Zero;
                _secret!.RevealAndUse((addrOfPtr: (IntPtr)(&ptr), pfnAllocator: (IntPtr)pfnAllocator), static (span, state) =>
                {
                    IntPtr ptr = ((delegate* managed<int, IntPtr>)state.pfnAllocator)(checked((span.Length + 1) * sizeof(char))); // will throw on OOM
                    Span<char> dest = new Span<char>((void*)ptr, span.Length + 1); // remainder of this method will not fail
                    span.CopyTo(dest);
                    dest[dest.Length - 1] = '\0';
                    *(IntPtr*)state.addrOfPtr = ptr;
                });
                Debug.Assert(ptr != IntPtr.Zero);
                return ptr;
            }
        }
    }
}
