// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;

namespace System.Security
{
    public unsafe sealed partial class SecureString : IDisposable
    {
        private const int MaxLength = 65536;
        private readonly object _methodLock = new object();
        private ShroudedBuffer<char>? _buffer;
        private bool _readOnly;

        public SecureString()
        {
            Initialize(ReadOnlySpan<char>.Empty);
        }

        [CLSCompliant(false)]
        public SecureString(char* value, int length)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_NeedNonNegNum);
            }
            if (length > MaxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length), SR.ArgumentOutOfRange_Length);
            }

            Initialize(new ReadOnlySpan<char>(value, length));
        }

        private void Initialize(ReadOnlySpan<char> value)
        {
            _buffer = new ShroudedBuffer<char>(value);
        }

        private SecureString(ShroudedBuffer<char> ownedBuffer)
        {
            _buffer = ownedBuffer;
        }

        public int Length
        {
            get
            {
                EnsureNotDisposed();
                return _buffer.Length;
            }
        }

        public void AppendChar(char c)
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();
                EnsureNotReadOnly();

                Span<char> newCharBuffer = GetNewCharBuffer(_buffer.Length + 1);
                fixed (char* pChars = newCharBuffer) // prevent the GC from moving this array
                {
                    try
                    {
                        _buffer.CopyTo(newCharBuffer);
                        newCharBuffer[^1] = c;
                        SetNewBufferUnderLock(newCharBuffer);
                    }
                    finally
                    {
                        newCharBuffer.Clear();
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

                SetNewBufferUnderLock(ReadOnlySpan<char>.Empty);
            }
        }

        // Do a deep-copy of the SecureString
        public SecureString Copy()
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();
                return new SecureString(_buffer.DeepClone());
            }
        }

        public void Dispose()
        {
            lock (_methodLock)
            {
                if (_buffer != null)
                {
                    _buffer.Dispose();
                    _buffer = null;
                }
            }
        }

        private char[] GetNewCharBuffer(int capacity)
        {
            if (capacity > MaxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), SR.ArgumentOutOfRange_Capacity);
            }

            return new char[capacity];
        }

        public void InsertAt(int index, char c)
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();
                EnsureNotReadOnly();

                if (index < 0 || index > _buffer.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_IndexString);
                }

                Span<char> charBuffer = GetNewCharBuffer(_buffer.Length + 1);
                fixed (char* pChars = charBuffer) // prevent the GC from moving this array
                {
                    try
                    {
                        _buffer.CopyTo(charBuffer);
                        charBuffer[index..^1].CopyTo(charBuffer.Slice(index + 1));
                        charBuffer[index] = c;
                        SetNewBufferUnderLock(charBuffer);
                    }
                    finally
                    {
                        charBuffer.Clear();
                    }
                }
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

                if (index < 0 || index >= _buffer.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_IndexString);
                }

                Span<char> charBuffer = GetNewCharBuffer(_buffer.Length);
                fixed (char* pChars = charBuffer) // prevent the GC from moving this array
                {
                    try
                    {
                        _buffer.CopyTo(charBuffer);
                        charBuffer.Slice(index + 1).CopyTo(charBuffer.Slice(index));
                        SetNewBufferUnderLock(charBuffer[..^1]);
                    }
                    finally
                    {
                        charBuffer.Clear();
                    }
                }
            }
        }

        public void SetAt(int index, char c)
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();
                EnsureNotReadOnly();

                if (index < 0 || index >= _buffer.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_IndexString);
                }

                Span<char> charBuffer = GetNewCharBuffer(_buffer.Length);
                fixed (char* pChars = charBuffer) // prevent the GC from moving this array
                {
                    try
                    {
                        _buffer.CopyTo(charBuffer);
                        charBuffer[index] = c;
                        SetNewBufferUnderLock(charBuffer);
                    }
                    finally
                    {
                        charBuffer.Clear();
                    }
                }
            }
        }

        private void SetNewBufferUnderLock(ReadOnlySpan<char> newContents)
        {
            ShroudedBuffer<char>? oldBuffer = _buffer;
            _buffer = new ShroudedBuffer<char>(newContents);
            oldBuffer?.Dispose();
        }

        private void EnsureNotReadOnly()
        {
            if (_readOnly)
            {
                throw new InvalidOperationException(SR.InvalidOperation_ReadOnly);
            }
        }

        [MemberNotNull(nameof(_buffer))]
        private void EnsureNotDisposed()
        {
            if (_buffer == null)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        internal IntPtr MarshalToBSTR()
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();

                IntPtr ptr = IntPtr.Zero;
                int length = _buffer.Length;
                try
                {
                    ptr = Marshal.AllocBSTR(length);
                    _buffer.CopyTo(new Span<char>((void*)ptr, length));

                    IntPtr result = ptr;
                    ptr = IntPtr.Zero; // so we don't free the new BSTR
                    return result;
                }
                finally
                {
                    // If we failed for any reason, free the new buffer
                    if (ptr != IntPtr.Zero)
                    {
                        new Span<char>((void*)ptr, length).Clear();
                        Marshal.FreeBSTR(ptr);
                    }
                }
            }
        }

        internal IntPtr MarshalToString(bool globalAlloc, bool unicode)
        {
            lock (_methodLock)
            {
                EnsureNotDisposed();

                // Always start by getting the contents of the string as unicode

                IntPtr ptrContentsAsUnicode = MarshalToStringUnicodeUnderLock(globalAlloc); // unmanaged buffer is terminated by a null char
                if (unicode)
                {
                    return ptrContentsAsUnicode;
                }

                // If we reached this point, we need to convert to ANSI.

                Span<char> spanContentsAsUnicode = new Span<char>((void*)ptrContentsAsUnicode, _buffer.Length); // span *does not* encapsulate terminating null
                int ansiLength = 0;
                IntPtr ptrContentsAsAnsi = IntPtr.Zero;

                try
                {
                    ansiLength = Marshal.GetAnsiStringByteCount(spanContentsAsUnicode); // byte count includes room for terminating null
                    ptrContentsAsAnsi = (globalAlloc) ? Marshal.AllocHGlobal(ansiLength) : Marshal.AllocCoTaskMem(ansiLength);
                    Marshal.GetAnsiStringBytes(spanContentsAsUnicode, new Span<byte>((void*)ptrContentsAsAnsi, ansiLength)); // auto-appends null terminator

                    IntPtr result = ptrContentsAsAnsi;
                    ptrContentsAsAnsi = IntPtr.Zero; // so we don't free the newly allocated memory
                    return result;
                }
                finally
                {
                    // Always free the unneeded Unicode buffer
                    spanContentsAsUnicode.Clear();
                    if (globalAlloc)
                    {
                        Marshal.FreeHGlobal(ptrContentsAsUnicode);
                    }
                    else
                    {
                        Marshal.FreeCoTaskMem(ptrContentsAsUnicode);
                    }

                    // If we failed for any reason, free the ANSI buffer
                    if (ptrContentsAsAnsi != IntPtr.Zero)
                    {
                        new Span<byte>((void*)ptrContentsAsAnsi, ansiLength).Clear();
                        if (globalAlloc)
                        {
                            Marshal.FreeHGlobal(ptrContentsAsAnsi);
                        }
                        else
                        {
                            Marshal.FreeCoTaskMem(ptrContentsAsAnsi);
                        }
                    }
                }
            }
        }

        private IntPtr MarshalToStringUnicodeUnderLock(bool globalAlloc)
        {
            Debug.Assert(_buffer != null);

            IntPtr ptr = IntPtr.Zero;
            int length = _buffer.Length;
            try
            {
                nuint byteLength = (nuint)sizeof(char) * (uint)checked(length + 1); // includes room for terminating null
                ptr = (globalAlloc) ? Marshal.AllocHGlobal((nint)byteLength) : Marshal.AllocCoTaskMem(checked((int)byteLength));
                _buffer.CopyTo(new Span<char>((void*)ptr, length));
                ((char*)ptr)[length] = '\0'; // append null terminator manually

                IntPtr result = ptr;
                ptr = IntPtr.Zero; // so we don't free the newly allocated memory
                return result;
            }
            finally
            {
                // If we failed for any reason, free the new buffer
                if (ptr != IntPtr.Zero)
                {
                    new Span<char>((void*)ptr, length).Clear();
                    if (globalAlloc)
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                    else
                    {
                        Marshal.FreeCoTaskMem(ptr);
                    }
                }
            }
        }
    }
}
