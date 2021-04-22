// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;

namespace System.Security
{
    /*
     * NOTE: This is a prototype implementation and is not representative of the final implementation.
     * It has rudimentary method implementations meant solely to flesh out the API surface.
     */

    public sealed class Secret<T> : ICloneable, IDisposable where T : unmanaged
    {
        private readonly T[] _data;
        private bool _disposed;

        public Secret(ReadOnlySpan<T> buffer)
        {
            _data = buffer.ToArray();
        }

        public int Length => _data.Length;

        public Secret<T> Clone()
        {
            if (_disposed) { throw new ObjectDisposedException(GetType().ToString()); }
            return new Secret<T>(_data);
        }

        public void Dispose()
        {
            _disposed = true;
        }

        public void UnshroudInto(Span<T> destination)
        {
            if (_disposed) { throw new ObjectDisposedException(GetType().ToString()); }
            _data.AsSpan().CopyTo(destination);
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }

    public static class SecretExtensions
    {
        public static T[] ToUnshroudedArray<T>(this Secret<T> secret) where T : unmanaged
        {
            T[] tmpArr = new T[secret.Length];
            secret.UnshroudInto(tmpArr);
            return tmpArr;
        }

        public static string ToUnshroudedString(this Secret<char> secret)
        {
            return string.Create(secret.Length, secret, (span, secret) =>
            {
                secret.UnshroudInto(span);
            });
        }

        public static void Use<T, TArg>(this Secret<T> secret, TArg arg, ReadOnlySpanAction<T, TArg> spanAction) where T : unmanaged
        {
            spanAction(ToUnshroudedArray(secret), arg);
        }
    }
}
