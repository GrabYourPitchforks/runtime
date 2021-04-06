// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Reflection
{
    public abstract partial class ConstructorInfo : MethodBase
    {
        // ConstructorInfo.Invoke(object[]) is called an order of magnitude more frequently than
        // ConstructorInfo.Invoke(BindingFlags, ...). Since the typical use case is that the actual
        // type is RuntimeConstructorInfo, we'll have the simple Invoke method be an inlineable wrapper around
        // the virtual workhorse method. If this isn't RCI, we'll double-dispatch back to the regular method
        // overload, but this should be rare enough that the perf hit shouldn't be a huge concern.

        [DebuggerHidden]
        [DebuggerStepThrough]
        public object Invoke(object?[]? parameters) => Invoke(parameters, default(InvocationOptions));

        [DebuggerHidden]
        [DebuggerStepThrough]
        private protected virtual object Invoke(object?[]? parameters, in InvocationOptions invokeOptions)
          => Invoke(invokeOptions.BindingFlags, invokeOptions.Binder, parameters, invokeOptions.Culture);

        internal virtual Type GetReturnType() { throw new NotImplementedException(); }
    }
}
