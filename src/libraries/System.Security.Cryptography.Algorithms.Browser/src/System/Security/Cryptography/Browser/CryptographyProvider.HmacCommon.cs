// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO;

namespace System.Security.Cryptography.Browser
{
    public abstract partial class CryptographyProvider
    {
        private delegate byte[] DigestFunction(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data);

        protected virtual bool CanDigestHmacSha1 => false;
        protected virtual byte[] DigestHmacSha1(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) => throw new NotSupportedException();

        protected virtual bool CanDigestHmacSha256 => false;
        protected virtual byte[] DigestHmacSha256(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) => throw new NotSupportedException();

        protected virtual bool CanDigestHmacSha384 => false;
        protected virtual byte[] DigestHmacSha384(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) => throw new NotSupportedException();

        protected virtual bool CanDigestHmacSha512 => false;
        protected virtual byte[] DigestHmacSha512(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data) => throw new NotSupportedException();

        private object[] CreateHmacCommon(string algorithmName, byte[] key)
        {
            bool isAlgorithmSupported = false;
            int digestSizeInBytes = 0;
            DigestFunction? digestFunc = null;

            switch (algorithmName)
            {
                case "SHA1":
                    digestSizeInBytes = 160 / 8;
                    isAlgorithmSupported = CanDigestHmacSha1;
                    digestFunc = DigestHmacSha1;
                    break;

                case "SHA256":
                    digestSizeInBytes = 256 / 8;
                    isAlgorithmSupported = CanDigestHmacSha256;
                    digestFunc = DigestHmacSha256;
                    break;

                case "SHA384":
                    digestSizeInBytes = 384 / 8;
                    isAlgorithmSupported = CanDigestHmacSha384;
                    digestFunc = DigestHmacSha384;
                    break;

                case "SHA512":
                    digestSizeInBytes = 512 / 8;
                    isAlgorithmSupported = CanDigestHmacSha512;
                    digestFunc = DigestHmacSha512;
                    break;
            }

            if (!isAlgorithmSupported)
            {
                throw new InvalidOperationException("Friendly error message here.");
            }

            Debug.Assert(digestFunc != null);
            HmacCommon hmac = new HmacCommon(key, digestFunc, digestSizeInBytes);
            return new object[]
            {
                (Func<int>)hmac.GetDigestSizeInBytes,
                (Action<ArraySegment<byte>>)hmac.HashCore,
                (Func<bool, byte[]>)hmac.GetCurrentHash,
                (Action)hmac.Dispose,
            };
        }

        private sealed class HmacCommon
        {
            private readonly int _digestSizeInBytes;
            private readonly DigestFunction _digestFunc;
            private readonly byte[] _key;
            private readonly MemoryStream _dataBuffer = new MemoryStream();

            internal HmacCommon(byte[] key, DigestFunction digestFunc, int digestSizeInBytes)
            {
                _key = (byte[])key.Clone();
                _digestFunc = digestFunc;
                _digestSizeInBytes = digestSizeInBytes;
            }

            internal int GetDigestSizeInBytes() => _digestSizeInBytes;

            internal void HashCore(ArraySegment<byte> data)
            {
                _dataBuffer.Write(data);
            }

            internal byte[] GetCurrentHash(bool reset)
            {
                if (!_dataBuffer.TryGetBuffer(out ArraySegment<byte> data))
                {
                    throw new InvalidOperationException(); // this should never happen
                }

                byte[] retVal = _digestFunc(_key, data);
                if (retVal.Length != _digestSizeInBytes)
                {
                    throw new InvalidOperationException(); // friendly error message here
                }

                if (reset)
                {
                    _dataBuffer.Position = 0;
                }
                return retVal;
            }

            internal void Dispose()
            {
                _dataBuffer.Dispose(); // release memory
            }
        }
    }
}
