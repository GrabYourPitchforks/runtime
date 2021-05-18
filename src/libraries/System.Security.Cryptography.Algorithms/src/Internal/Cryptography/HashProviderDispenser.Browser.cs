// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;

namespace Internal.Cryptography
{
    internal static partial class HashProviderDispenser
    {
        public static HashProvider CreateHashProvider(string hashAlgorithmId)
        {
            switch (hashAlgorithmId)
            {
                case HashAlgorithmNames.SHA1:
                case HashAlgorithmNames.SHA256:
                case HashAlgorithmNames.SHA384:
                case HashAlgorithmNames.SHA512:
                    return new SHAHashProvider(hashAlgorithmId);
            }
            throw new CryptographicException(SR.Format(SR.Cryptography_UnknownHashAlgorithm, hashAlgorithmId));
        }

        public static class OneShotHashProvider
        {
            public static int HashData(string hashAlgorithmId, ReadOnlySpan<byte> source, Span<byte> destination)
            {
                HashProvider provider = HashProviderDispenser.CreateHashProvider(hashAlgorithmId);
                provider.AppendHashData(source);
                return provider.FinalizeHashAndReset(destination);
            }
        }

        public static unsafe HashProvider CreateMacProvider(string hashAlgorithmId, ReadOnlySpan<byte> key)
        {
            switch (hashAlgorithmId)
            {
                case HashAlgorithmNames.SHA1:
                case HashAlgorithmNames.SHA256:
                case HashAlgorithmNames.SHA384:
                case HashAlgorithmNames.SHA512:
                    return new PolyfillHmacProvider(hashAlgorithmId, key);
            }

            throw new PlatformNotSupportedException(SR.SystemSecurityCryptographyAlgorithms_PlatformNotSupported);
        }

        private sealed class PolyfillHmacProvider : HashProvider
        {
            private readonly object[] _polyfillVtable;

            internal PolyfillHmacProvider(string hashAlgorithmId, ReadOnlySpan<byte> key)
            {
                _polyfillVtable = CryptographyProvider.GetHmacImplementation(hashAlgorithmId, key.ToArray());
            }

            public override int HashSizeInBytes
            {
                get
                {
                    return ((Func<int>)_polyfillVtable[(int)PolyfillVtableEntries.GetDigestSizeInBytes])();
                }
            }

            public override void AppendHashData(ReadOnlySpan<byte> data)
            {
                ((Action<byte[]>)_polyfillVtable[(int)PolyfillVtableEntries.HashCore])(data.ToArray());
            }

            public override void Dispose(bool disposing)
            {
                ((Action)_polyfillVtable[(int)PolyfillVtableEntries.Dispose])();
            }

            public override int FinalizeHashAndReset(Span<byte> destination) => GetHash(destination, reset: true);

            public override int GetCurrentHash(Span<byte> destination) => GetHash(destination, reset: false);

            private int GetHash(Span<byte> destination, bool reset)
            {
                byte[] digest = ((Func<bool, byte[]>)_polyfillVtable[(int)PolyfillVtableEntries.GetCurrentHash])(reset);
                digest.CopyTo(destination);
                return digest.Length;
            }

            private enum PolyfillVtableEntries
            {
                GetDigestSizeInBytes, // Func<int>
                HashCore, // Action<ArraySegment<byte>>
                GetCurrentHash, // Func<bool, byte[]>
                Dispose, // Action
            }
        }
    }
}
