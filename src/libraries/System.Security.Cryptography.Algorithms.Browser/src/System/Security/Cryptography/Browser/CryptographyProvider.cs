// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Security.Cryptography.Browser
{
    public abstract partial class CryptographyProvider
    {
        private const string AppDomainDataKey = "System.Security.Cryptography.Browser::polyfill";
        private const int Version = 0x20210518;

        protected CryptographyProvider() { }

        public static void Install(CryptographyProvider provider)
        {
            if (AppDomain.CurrentDomain.GetData(AppDomainDataKey) is not null)
            {
                throw new InvalidOperationException(SR.CryptographyProvider_AlreadyInitialized);
            }

            Dictionary<string, object> polyfill = new()
            {
                [nameof(Version)] = Version,
                [nameof(CreateHmacCommon)] = (Func<string, byte[], object[]>)provider.CreateHmacCommon,
            };

            AppDomain.CurrentDomain.SetData(AppDomainDataKey, polyfill);
        }
    }
}
