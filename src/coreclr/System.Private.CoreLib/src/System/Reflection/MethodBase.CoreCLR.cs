// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Internal.Runtime.CompilerServices;

namespace System.Reflection
{
    public abstract partial class MethodBase : MemberInfo
    {
        // MethodBase.Invoke(object, object[]) is called an order of magnitude more frequently than
        // MethodBase.Invoke(object, BindingFlags, ...). Since the typical use case is that the actual
        // type is RuntimeMethodInfo or RuntimeConstructorInfo, we'll have the simple Invoke method be
        // an inlineable wrapper around the virtual workhorse method. If this isn't a RMI or RCI, we'll
        // double-dispatch back to the regular method overload, but this should be rare enough that
        // the perf hit shouldn't be a huge concern.

        [DebuggerHidden]
        [DebuggerStepThrough]
        public object? Invoke(object? obj, object?[]? parameters) => Invoke(obj, parameters, default(InvocationOptions));

        [DebuggerHidden]
        [DebuggerStepThrough]
        private protected virtual object? Invoke(object? obj, object?[]? parameters, in InvocationOptions invokeOptions)
          => Invoke(obj, invokeOptions.BindingFlags, invokeOptions.Binder, parameters, invokeOptions.Culture);

        #region Static Members
        public static MethodBase? GetMethodFromHandle(RuntimeMethodHandle handle)
        {
            if (handle.IsNullHandle())
                throw new ArgumentException(SR.Argument_InvalidHandle);

            MethodBase? m = RuntimeType.GetMethodBase(handle.GetMethodInfo());

            Type? declaringType = m?.DeclaringType;
            if (declaringType != null && declaringType.IsGenericType)
                throw new ArgumentException(SR.Format(
                    SR.Argument_MethodDeclaringTypeGeneric,
                    m, declaringType.GetGenericTypeDefinition()));

            return m;
        }

        public static MethodBase? GetMethodFromHandle(RuntimeMethodHandle handle, RuntimeTypeHandle declaringType)
        {
            if (handle.IsNullHandle())
                throw new ArgumentException(SR.Argument_InvalidHandle);

            return RuntimeType.GetMethodBase(declaringType.GetRuntimeType(), handle.GetMethodInfo());
        }

        [System.Security.DynamicSecurityMethod] // Methods containing StackCrawlMark local var has to be marked DynamicSecurityMethod
        public static MethodBase? GetCurrentMethod()
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeMethodInfo.InternalGetCurrentMethod(ref stackMark);
        }
        #endregion

        #region Internal Members
        // used by EE
        private IntPtr GetMethodDesc() { return MethodHandle.Value; }

        internal virtual ParameterInfo[] GetParametersNoCopy() { return GetParameters(); }
        #endregion

        #region Internal Methods
        // helper method to construct the string representation of the parameter list

        internal virtual Type[] GetParameterTypes()
        {
            ParameterInfo[] paramInfo = GetParametersNoCopy();

            Type[] parameterTypes = new Type[paramInfo.Length];
            for (int i = 0; i < paramInfo.Length; i++)
                parameterTypes[i] = paramInfo[i].ParameterType;

            return parameterTypes;
        }

        private protected Span<object?> CheckArguments(ref StackAllocedArguments stackArgs, object?[]? parameters, Signature sig, in InvocationOptions invokeOptions)
        {
            Debug.Assert(Unsafe.SizeOf<StackAllocedArguments>() == StackAllocedArguments.MaxStackAllocArgCount * Unsafe.SizeOf<object>(),
                "MaxStackAllocArgCount not properly defined.");

            Span<object?> copyOfParameters = default;

            if (parameters is not null)
            {
                // copy the arguments into a temporary buffer (or a new array) so we detach from any user changes
                copyOfParameters = (parameters.Length <= StackAllocedArguments.MaxStackAllocArgCount)
                        ? MemoryMarshal.CreateSpan(ref stackArgs._arg0, parameters.Length)
                        : new Span<object?>(new object?[parameters.Length]);

                ParameterInfo[]? p = null;
                for (int i = 0; i < parameters.Length; i++)
                {
                    object? arg = parameters[i];
                    RuntimeType argRT = sig.Arguments[i];

                    if (arg == Type.Missing)
                    {
                        p ??= GetParametersNoCopy();
                        if (p[i].DefaultValue == System.DBNull.Value)
                            throw new ArgumentException(SR.Arg_VarMissNull, nameof(parameters));
                        arg = p[i].DefaultValue!;
                    }
                    copyOfParameters[i] = argRT.CheckValue(arg, invokeOptions);
                }
            }

            return copyOfParameters;
        }

        // Helper struct to avoid intermediate object[] allocation in calls to the native reflection stack.
        // Typical usage is to define a local of type default(StackAllocedArguments), then pass 'ref theLocal'
        // as the first parameter to CheckArguments. CheckArguments will try to utilize storage within this
        // struct instance if there's sufficient space; otherwise CheckArguments will allocate a temp array.
        private protected struct StackAllocedArguments
        {
            internal const int MaxStackAllocArgCount = 8;
            internal object? _arg0;
#pragma warning disable CA1823, CS0169, IDE0051 // accessed via 'CheckArguments' ref arithmetic
            private object? _arg1;
            private object? _arg2;
            private object? _arg3;
            private object? _arg4;
            private object? _arg5;
            private object? _arg6;
            private object? _arg7;
#pragma warning restore CA1823, CS0169, IDE0051
        }
        #endregion
    }
}
