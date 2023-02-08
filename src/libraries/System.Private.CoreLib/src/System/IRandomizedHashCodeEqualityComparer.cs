// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace System
{
    internal interface IRandomizedHashCodeEqualityComparer<in T> : IEqualityComparer<T>
    {
        int GetRandomizedHashCode([DisallowNull] T obj);
    }
}
