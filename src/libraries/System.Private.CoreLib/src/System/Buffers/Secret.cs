// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Internal.Runtime.CompilerServices;

namespace System.Buffers
{
    public unsafe sealed class Secret<T> : IDisposable where T : unmanaged
    {
        // Use of SafeHandle here puts the runtime in control of ref counting
        // and multithreading, so we don't have to worry about one thread using
        // the value while another thread is concurrently disposing the handle.
        internal readonly SecretSafeHandle _secretHandle;

        public Secret(ReadOnlySpan<T> buffer)
        {
            // The 'checked' expression below should never fail (since otherwise we'd have an illegal span),
            // but may as well leave it in since it's not hurting anything.

            nuint expectedByteLength = checked((nuint)sizeof(T) * (uint)buffer.Length);
            SecretSafeHandle secretHandle = SecretSafeHandle.Allocate(expectedByteLength);

            bool success = false;
            try
            {
                secretHandle.DangerousAddRef(ref success);
                secretHandle.DangerousGetRawData(out nuint actualByteLength, out void* pData);
                Debug.Assert(expectedByteLength == actualByteLength);

                // n.b. pData could reference an address misaligned for 'T', so we should fall back
                // to a normal binary memmove rather than use ReadOnlySpan<T>.CopyTo.
                Buffer.Memmove(
                    dest: ref *(byte*)pData,
                    src: ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(buffer)),
                    len: expectedByteLength);
            }
            finally
            {
                if (success)
                {
                    secretHandle.DangerousRelease();
                }
            }

            _secretHandle = secretHandle;
        }

        // ctor which takes ownership of the handle
        private Secret(SecretSafeHandle secretHandle)
        {
            Debug.Assert(secretHandle is not null);
            _secretHandle = secretHandle;
        }

        public int GetLength()
        {
            bool success = false;
            try
            {
                _secretHandle.DangerousAddRef(ref success);
                _secretHandle.DangerousGetRawData(out nuint actualByteLength, out _);
                (nuint quotient, nuint remainder) = Math.DivRem(actualByteLength, (nuint)sizeof(T));
                Debug.Assert(remainder == 0, "We somehow have a partial T?");
                Debug.Assert(quotient <= int.MaxValue, "We somehow have more than int.MaxValue T elements?");
                return (int)quotient;
            }
            finally
            {
                if (success)
                {
                    _secretHandle.DangerousRelease();
                }
            }
        }

        public Secret<T> Clone() => new Secret<T>(_secretHandle.Duplicate());

        public void Dispose() => _secretHandle.Dispose();

        public int RevealInto(Span<T> destination)
        {
            bool success = false;
            try
            {
                _secretHandle.DangerousAddRef(ref success);
                _secretHandle.DangerousGetRawData(out nuint actualByteLength, out void* pData);
                (nuint quotient, nuint remainder) = Math.DivRem(actualByteLength, (nuint)sizeof(T));
                Debug.Assert(remainder == 0, "We somehow have a partial T?");
                Debug.Assert(quotient <= int.MaxValue, "We somehow have more than int.MaxValue T elements?");

                if (quotient > (nuint)destination.Length)
                {
                    ThrowHelper.ThrowArgumentException_DestinationTooShort();
                }

                // n.b. pData could reference an address misaligned for 'T', so we should fall back
                // to a normal binary memmove rather than use ReadOnlySpan<T>.CopyTo.
                Buffer.Memmove(
                    dest: ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(destination)),
                    src: ref *(byte*)pData,
                    len: actualByteLength);

                return (int)quotient;
            }
            finally
            {
                if (success)
                {
                    _secretHandle.DangerousRelease();
                }
            }
        }

        public T[] RevealToArray()
        {
            bool success = false;
            try
            {
                _secretHandle.DangerousAddRef(ref success);
                _secretHandle.DangerousGetRawData(out nuint actualByteLength, out void* pData);
                (nuint quotient, nuint remainder) = Math.DivRem(actualByteLength, (nuint)sizeof(T));
                Debug.Assert(remainder == 0, "We somehow have a partial T?");
                Debug.Assert(quotient <= int.MaxValue, "We somehow have more than int.MaxValue T elements?");

                int elementCount = (int)quotient;
                T[] newArr = GC.AllocateUninitializedArray<T>(elementCount, pinned: true);

                // n.b. pData could reference an address misaligned for 'T', so we should fall back
                // to a normal binary memmove rather than use ReadOnlySpan<T>.CopyTo.
                Buffer.Memmove(
                    dest: ref Unsafe.As<T, byte>(ref MemoryMarshal.GetArrayDataReference(newArr)),
                    src: ref *(byte*)pData,
                    len: actualByteLength);

                return newArr;
            }
            finally
            {
                if (success)
                {
                    _secretHandle.DangerousRelease();
                }
            }
        }

        public void RevealAndUse<TArg>(TArg arg, ReadOnlySpanAction<T, TArg> spanAction)
        {
            if (spanAction is null)
            {
                throw new ArgumentNullException(nameof(spanAction));
            }

            const int MAX_STACKALLOC_BYTES = 4096;
            int MAX_STACKALLOC_T = MAX_STACKALLOC_BYTES / sizeof(T);

            bool success = false;
            try
            {
                _secretHandle.DangerousAddRef(ref success);
                _secretHandle.DangerousGetRawData(out nuint actualByteLength, out void* pData);
                (nuint quotient, nuint remainder) = Math.DivRem(actualByteLength, (nuint)sizeof(T));
                Debug.Assert(remainder == 0, "We somehow have a partial T?");
                Debug.Assert(quotient <= int.MaxValue, "We somehow have more than int.MaxValue T elements?");

                int elementCount = (int)quotient;
                Span<T> temp = (elementCount <= MAX_STACKALLOC_T) ? stackalloc T[MAX_STACKALLOC_T] : new T[elementCount];

                // Slice in case we stackalloced; pin in case we allocated from the heap.
                // Do not use the array pool. If the application has a bug which results in improper array pool access,
                // our use of the array pool could manifest as information disclosure. We're defensive and will take
                // the allocation hit here. Honestly this should be fine, as we expect the vast majority of secrets
                // to be within the stackalloc size limit defined above.

                temp = temp.Slice(0, elementCount);
                fixed (T* ptrTemp = &MemoryMarshal.GetReference(temp))
                {
                    try
                    {
                        // n.b. pData could reference an address misaligned for 'T', so we should fall back
                        // to a normal binary memmove rather than use ReadOnlySpan<T>.CopyTo.
                        Buffer.Memmove(
                            dest: ref *(byte*)ptrTemp,
                            src: ref *(byte*)pData,
                            len: actualByteLength);
                        spanAction(temp, arg);
                    }
                    finally
                    {
                        temp.Clear(); // don't leave sensitive data lying around
                    }
                }
            }
            finally
            {
                if (success)
                {
                    _secretHandle.DangerousRelease();
                }
            }
        }

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        [Obsolete("Use RevealToString")]
        [EditorBrowsable(EditorBrowsableState.Never)] // we don't want people to call this
        public override string ToString() => base.ToString()!;
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    }

    public static class Secret
    {
        public static Secret<char> Create(string value)
        {
            if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
            return new Secret<char>(value);
        }

        public static Secret<T> Create<T>(T[] buffer) where T : unmanaged
        {
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
            return new Secret<T>(buffer);
        }

        public static Secret<T> Create<T>(ReadOnlySpan<T> buffer) where T : unmanaged
            => new Secret<T>(buffer);

        public static unsafe string RevealToString(this Secret<char> secret)
        {
            if (secret is null)
            {
                throw new ArgumentNullException(nameof(secret));
            }

            SecretSafeHandle secretHandle = secret._secretHandle;

            bool success = false;
            try
            {
                secretHandle.DangerousAddRef(ref success);
                secretHandle.DangerousGetRawData(out nuint actualByteLength, out void* pData);
                (nuint quotient, nuint remainder) = Math.DivRem(actualByteLength, sizeof(char));
                Debug.Assert(remainder == 0, "We somehow have a partial T?");
                Debug.Assert(quotient <= int.MaxValue, "We somehow have more than int.MaxValue T elements?");

                // n.b. pData could be misaligned for 'char', and the string ctors aren't explicitly
                // documented as allowing misaligned input. We'll instead perform a binary memmove.
                return string.Create((int)quotient, (IntPtr)pData, (span, state) =>
                {
                    Buffer.Memmove(
                        dest: ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(span)),
                        src: ref *(byte*)state,
                        len: (uint)span.Length * (nuint)sizeof(char));
                });
            }
            finally
            {
                if (success)
                {
                    secretHandle.DangerousRelease();
                }
            }
        }
    }
}
