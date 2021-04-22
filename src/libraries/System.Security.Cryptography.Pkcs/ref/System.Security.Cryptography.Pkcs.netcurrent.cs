// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// ------------------------------------------------------------------------------
// Changes to this file must follow the https://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.Security.Cryptography.Pkcs
{
    public sealed partial class Pkcs12Builder
    {
        public void AddSafeContentsEncrypted(System.Security.Cryptography.Pkcs.Pkcs12SafeContents safeContents, System.Security.Secret<byte>? passwordBytes, System.Security.Cryptography.PbeParameters pbeParameters) { }
        public void AddSafeContentsEncrypted(System.Security.Cryptography.Pkcs.Pkcs12SafeContents safeContents, System.Security.Secret<char>? password, System.Security.Cryptography.PbeParameters pbeParameters) { }
        public void SealWithMac(System.Security.Secret<char>? password, System.Security.Cryptography.HashAlgorithmName hashAlgorithm, int iterationCount) { }
    }
    public sealed partial class Pkcs12Info
    {
        public bool VerifyMac(System.Security.Secret<char>? password) { throw null; }
    }
    public sealed partial class Pkcs12SafeContents
    {
        public System.Security.Cryptography.Pkcs.Pkcs12ShroudedKeyBag AddShroudedKey(System.Security.Cryptography.AsymmetricAlgorithm key, System.Security.Secret<byte>? passwordBytes, System.Security.Cryptography.PbeParameters pbeParameters) { throw null; }
        public System.Security.Cryptography.Pkcs.Pkcs12ShroudedKeyBag AddShroudedKey(System.Security.Cryptography.AsymmetricAlgorithm key, System.Security.Secret<char>? password, System.Security.Cryptography.PbeParameters pbeParameters) { throw null; }
        public void Decrypt(System.Security.Secret<byte>? passwordBytes) { }
        public void Decrypt(System.Security.Secret<char>? password) { }
    }
    public sealed partial class Pkcs8PrivateKeyInfo
    {
        public static System.Security.Cryptography.Pkcs.Pkcs8PrivateKeyInfo DecryptAndDecode(System.Security.Secret<byte>? passwordBytes, System.ReadOnlyMemory<byte> source, out int bytesRead) { throw null; }
        public static System.Security.Cryptography.Pkcs.Pkcs8PrivateKeyInfo DecryptAndDecode(System.Security.Secret<char>? password, System.ReadOnlyMemory<byte> source, out int bytesRead) { throw null; }
        public byte[] Encrypt(System.Security.Secret<byte>? passwordBytes, System.Security.Cryptography.PbeParameters pbeParameters) { throw null; }
        public byte[] Encrypt(System.Security.Secret<char>? password, System.Security.Cryptography.PbeParameters pbeParameters) { throw null; }
        public bool TryEncrypt(System.Security.Secret<byte>? passwordBytes, System.Security.Cryptography.PbeParameters pbeParameters, System.Span<byte> destination, out int bytesWritten) { throw null; }
        public bool TryEncrypt(System.Security.Secret<char>? password, System.Security.Cryptography.PbeParameters pbeParameters, System.Span<byte> destination, out int bytesWritten) { throw null; }
    }
}
