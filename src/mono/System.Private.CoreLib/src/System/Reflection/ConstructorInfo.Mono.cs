// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Reflection
{
    public partial class ConstructorInfo
    {
        [DebuggerHidden]
        [DebuggerStepThrough]
        public object Invoke(object?[]? parameters) => Invoke(BindingFlags.Default, binder: null, parameters: parameters, culture: null);
    }
}
