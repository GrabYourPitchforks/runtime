// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// ------------------------------------------------------------------------------
// Changes to this file must follow the https://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Security.Cryptography.Browser
{
    public abstract class CryptographyProvider
    {
        protected CryptographyProvider() { }

        public static void Install(CryptographyProvider provider) { throw null; }

        protected virtual bool CanDigestHmacSha1 { get { throw null; } }
        protected virtual byte[] DigestHmacSha1(System.ReadOnlySpan<byte> key, System.ReadOnlySpan<byte> data) { throw null; }
    }
}
