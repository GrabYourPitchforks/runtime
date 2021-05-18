// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Internal.Cryptography
{
    internal static class CryptographyProvider
    {
        private const string AppDomainDataKey = "System.Security.Cryptography.Browser::polyfill";
        private const int Version = 0x20210518;

        private static Dictionary<string, object>? _polyfill;
        private static Dictionary<string, object> GetPolyfill()
        {
            if (_polyfill is null)
            {
                Dictionary<string, object>? polyfill = AppDomain.CurrentDomain.GetData(AppDomainDataKey) as Dictionary<string, object>;
                if (polyfill is null
                    || !polyfill.TryGetValue(nameof(Version), out object? embeddedVersion)
                    || !Version.Equals(embeddedVersion)) // ensure polyfill assembly in sync with us
                {
                    throw new InvalidOperationException("Some helpful text as to how to configure the polyfill.");
                }
                _polyfill = polyfill;
            }
            return _polyfill;
        }

        internal static object[] GetHmacImplementation(string algorithmName, byte[] key)
        {
            return ((Func<string, byte[], object[]>)GetPolyfill()["GetHmacCommon"])(algorithmName, key);
        }
    }
}
