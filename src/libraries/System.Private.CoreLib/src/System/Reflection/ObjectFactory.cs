// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Reflection
{
    /// <summary>
    /// A factory for object instances, used by <see cref="Activator.CreateInstance"/>,
    /// <see cref="RuntimeType.CreateInstanceDefaultCtor"/>, and related APIs.
    /// </summary>
    internal sealed unsafe partial class ObjectFactory
    {
        // The managed calli to the newobj allocator, plus its first argument (MethodTable*).
        // In the case of the COM allocator, first arg is ComClassFactory*, not MethodTable*.
        private readonly delegate*<void*, object?> _pfnAllocator;
        private readonly void* _allocatorFirstArg;

        // The managed calli to the parameterless ctor, taking "this" (as object) as its first argument.
        private readonly delegate*<object?, void> _pfnCtor;

        // Any interesting details about how instantiations of this type should be performed.
        private readonly FactoryFlags _flags = FactoryFlags.None;

        // Needed so we can perform keepalives during object allocation.
        private readonly RuntimeType _runtimeType;

        internal ObjectFactory(RuntimeType type)
        {
            Debug.Assert(type is not null);

            _runtimeType = type;

            // The check below is redundant since these same checks are performed at the
            // unmanaged layer, but this call will throw slightly different exceptions
            // than the unmanaged layer, and callers might be dependent on this.

            type.CreateInstanceCheckThis();

            try
            {
                RuntimeTypeHandle.GetActivationInfo(type,
                    out _pfnAllocator!, out _allocatorFirstArg,
                    out _pfnCtor!, out bool ctorIsPublic);

                if (ctorIsPublic)
                {
                    _flags |= FactoryFlags.CtorIsPublic;
                }
            }
            catch (Exception ex)
            {
                // Exception messages coming from the runtime won't include
                // the type name. Let's include it here to improve the
                // debugging experience for our callers.

                string friendlyMessage = SR.Format(SR.Activator_CannotCreateInstance, type, ex.Message);
                switch (ex)
                {
                    case ArgumentException: throw new ArgumentException(friendlyMessage);
                    case PlatformNotSupportedException: throw new PlatformNotSupportedException(friendlyMessage);
                    case NotSupportedException: throw new NotSupportedException(friendlyMessage);
                    case MethodAccessException: throw new MethodAccessException(friendlyMessage);
                    case MissingMethodException: throw new MissingMethodException(friendlyMessage);
                    case MemberAccessException: throw new MemberAccessException(friendlyMessage);
                }

                throw; // can't make a friendlier message, rethrow original exception
            }

            // Activator.CreateInstance and friends return null given typeof(Nullable<T>).

            if (_pfnAllocator == null)
            {
                Debug.Assert(Nullable.GetUnderlyingType(type) is not null,
                    "Null allocator should only be returned for Nullable<T>.");

                static object? ReturnNull(void* _) => null;
                _pfnAllocator = &ReturnNull;
            }

            // If no ctor is provided, we have Nullable<T>, a ctorless value type T,
            // or a ctorless __ComObject. In any case, we should replace the
            // ctor call with our no-op stub. The unmanaged GetActivationInfo layer
            // would have thrown an exception if 'rt' were a normal reference type
            // without a ctor.

            if (_pfnCtor == null)
            {
                Debug.Assert(CtorIsPublic); // implicit parameterless ctor is always considered public

                static void CtorNoopStub(object? uninitializedObject) { }
                _pfnCtor = &CtorNoopStub; // we use null singleton pattern if no ctor call is necessary
            }

            // We don't need to worry about invoking cctors here. The runtime will figure it
            // out for us when the instance ctor is called. For value types, because we're
            // creating a boxed default(T), the static cctor is called when *any* instance
            // method is invoked.
        }

        internal bool CtorIsPublic => (_flags & FactoryFlags.CtorIsPublic) != 0;

        // Aggressive inlining because hot code paths call this method directly
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [StackTraceHidden]
        internal void CallConstructor(object? uninitializedObject) => _pfnCtor(uninitializedObject);

        [StackTraceHidden]
        private object? CreateInitializedObject()
        {
            object? retVal = CreateUninitializedObject();
            CallConstructor(retVal);
            return retVal;
        }

        // Aggressive inlining because hot code paths call this method directly
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [StackTraceHidden]
        internal object? CreateUninitializedObject()
        {
            // We don't use the captured RuntimeType directly, but we need to keep
            // it alive until the object is allocated. Once allocated, the object
            // itself will keep the target type / assembly alive.

            object? retVal = _pfnAllocator(_allocatorFirstArg);
            GC.KeepAlive(_runtimeType);
            return retVal;
        }

        internal Func<object?> GetInitializedObjectFactory()
        {
            if ((_flags & FactoryFlags.TypeHasNoAllocator) != 0)
            {
                return ReturnNull;
            }
            else if ((_flags & FactoryFlags.TypeHasNoConstructor) != 0)
            {
                return CreateUninitializedObject;
            }
            else
            {
                return CreateInitializedObject;
            }
        }

        private object? ReturnNull() => null;

        [Flags]
        private enum FactoryFlags
        {
            None = 0,
            CtorIsPublic = 1 << 0,
            TypeHasNoAllocator = 1 << 1,
            TypeHasNoConstructor = 1 << 2,
        }
    }
}
