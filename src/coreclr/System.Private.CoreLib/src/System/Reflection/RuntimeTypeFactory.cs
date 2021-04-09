// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Reflection
{
    internal sealed class RuntimeTypeFactory
    {
        private static readonly Dictionary<int, WeakReference<CachedStubEntry>[]> s_cachedStubEntries = new();

        private static readonly RuntimeType?[] s_normalizedTypes = new RuntimeType?[]
        {
            null,
            (RuntimeType)typeof(void),
            (RuntimeType)typeof(byte).MakeByRefType(),
            (RuntimeType)typeof(byte),
            (RuntimeType)typeof(ushort),
            (RuntimeType)typeof(uint),
            (RuntimeType)typeof(ulong),
            (RuntimeType)typeof(float),
            (RuntimeType)typeof(double),
        };

        internal static unsafe Delegate CreateFactory(RuntimeType delegateType, RuntimeConstructorInfo ctorInfo)
        {
            // Caller should already have validated that the requested delegate type
            // is compatible with the ctor info we're using.

            ShuffleType shuffleType;
            if (RuntimeTypeHandle.IsValueType((RuntimeType)ctorInfo.DeclaringType!))
            {
                RuntimeMethodInfo invokeMethod = (RuntimeMethodInfo)delegateType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Static)!;
                if (RuntimeTypeHandle.IsValueType((RuntimeType)invokeMethod.ReturnType))
                {
                    shuffleType = ShuffleType.ValueTypeCtorReturnedByValue;
                }
                else
                {
                    shuffleType = ShuffleType.ValueTypeCtorReturnedBoxed;
                }
            }
            else
            {
                shuffleType = ShuffleType.RefTypeCtor;
            }

            CachedStubEntry realStubEntry = GetOrAddStubEntry(new CachedStubEntry(shuffleType, ctorInfo.Signature));

            // Get the allocator, method table, and ctor pointer for this object

            State state = new State()
            {
                cachedStubEntry = realStubEntry,
                ctorDeclaredType = (RuntimeType)ctorInfo.DeclaringType!,
                pfnCtor = ctorInfo.MethodHandle.GetFunctionPointer()
            };

            RuntimeTypeHandle.GetActivationInfo(state.ctorDeclaredType, out state.pfnAlloc, out state.pAllocFirstArg, out _, out _);

            // Compile the stub method if it hasn't already been compiled, then get its fnptr and
            // wrap the delegate around it.

            RuntimeMethodHandle stubHandle = realStubEntry.GetOrCreateStub();
            IntPtr stubFnptr = stubHandle.GetFunctionPointer();

            RuntimeConstructorInfo delegateCtor = (RuntimeConstructorInfo)delegateType.GetConstructor(BindingFlags.Instance | BindingFlags.Public, new Type[] { typeof(object), typeof(IntPtr) })!;
            Delegate retVal = (Delegate)delegateCtor.Invoke(new object[] { state, stubFnptr });

            GC.KeepAlive(stubHandle.GetMethodInfo());
            return retVal;
        }

        private static CachedStubEntry GetOrAddStubEntry(CachedStubEntry stubEntry)
        {
            int hashCode = stubEntry.GetHashCode(); // compute outside lock

            // First, see if it already exists in the dictionary

            WeakReference<CachedStubEntry>[]? existingDictEntry;
            lock (s_cachedStubEntries)
            {
                s_cachedStubEntries.TryGetValue(hashCode, out existingDictEntry);
            }

            if (existingDictEntry is not null)
            {
                // Enumerate entries, looking for the one that matches ours
                foreach (WeakReference<CachedStubEntry> element in existingDictEntry)
                {
                    if (!element.TryGetTarget(out CachedStubEntry? innerEntry))
                    {
                        break; // GC started empting entries; need to clean the cache
                    }

                    if (stubEntry.Equals(innerEntry))
                    {
                        return innerEntry; // found a match!
                    }
                }
            }

            // If we reached this point, we need to clear dead weight from the cache,
            // or we need to add ourselves to the cache. This should be a fairly rare
            // operation, so it's ok to perform this under lock.

            lock (s_cachedStubEntries)
            {
                List<int>? keysToRemove = null;
                Dictionary<int, WeakReference<CachedStubEntry>[]>? keysToReplace = null;
                List<WeakReference<CachedStubEntry>>? tempList = new List<WeakReference<CachedStubEntry>>();

                foreach (var kvp in s_cachedStubEntries)
                {
                    tempList.Clear();

                    foreach (var innerEntry in kvp.Value)
                    {
                        if (innerEntry.TryGetTarget(out _))
                        {
                            tempList.Add(innerEntry); // target was still live
                        }
                    }

                    if (tempList.Count == 0)
                    {
                        // none of the original targets are still alive; remove the entire KVP entry
                        (keysToRemove ??= new()).Add(kvp.Key);
                    }
                    else if (tempList.Count < kvp.Value.Length)
                    {
                        // some of the original targets are still alive; trim (but don't remove) the KVP entry
                        (keysToReplace ??= new())[kvp.Key] = tempList.ToArray();
                    }
                }

                if (keysToRemove is not null)
                {
                    foreach (int key in keysToRemove)
                    {
                        s_cachedStubEntries.Remove(key);
                    }
                }

                if (keysToReplace is not null)
                {
                    foreach (var kvp in keysToReplace)
                    {
                        s_cachedStubEntries[kvp.Key] = kvp.Value;
                    }
                }

                // Try to find ourselves in the cache once more

                if (s_cachedStubEntries.TryGetValue(hashCode, out WeakReference<CachedStubEntry>[]? existingArray))
                {
                    foreach (var element in existingArray)
                    {
                        if (element.TryGetTarget(out CachedStubEntry? innerEntry)
                            && stubEntry.Equals(innerEntry))
                        {
                            return innerEntry; // already exists and found a match!
                        }
                    }
                }

                // And if we're not there, add and return ourselves

                Array.Resize(ref existingArray, (existingArray?.Length ?? 0) + 1);
                existingArray[existingArray.Length - 1] = new WeakReference<CachedStubEntry>(stubEntry);
                s_cachedStubEntries[hashCode] = existingArray;
                return stubEntry;
            }
        }

        private sealed class CachedStubEntry : IEquatable<CachedStubEntry>
        {
            private RuntimeMethodHandle? _stub;

            internal CachedStubEntry(ShuffleType shuffleType, Signature realSignature)
            {
                CallingConvention = realSignature.CallingConvention;
                NormalizedCtorArgs = Array.ConvertAll(realSignature.Arguments, GetNormalizedType);
                ShuffleType = shuffleType;

                if (ShuffleType == ShuffleType.ValueTypeCtorReturnedByValue)
                {
                    DeclaringValueType = realSignature.m_declaringType;
                }
            }

            internal RuntimeType? DeclaringValueType { get; }
            internal RuntimeType[] NormalizedCtorArgs { get; }
            internal CallingConventions CallingConvention { get; }
            internal ShuffleType ShuffleType { get; }

            private string CreateDynamicMethodNameString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(DeclaringValueType is not null ? DeclaringValueType.Name : "Shared");
                sb.Append("::.ctor_");
                sb.Append(ShuffleType);
                sb.Append('_');
                sb.Append(CallingConvention);
                foreach (RuntimeType type in NormalizedCtorArgs)
                {
                    sb.Append('_');
                    sb.Append(type.Name); // "Int32", "Byte&", etc.
                }
                return sb.ToString();
            }

            private RuntimeMethodHandle CreateStub()
            {
                RuntimeType[] parameterTypes = new RuntimeType[NormalizedCtorArgs.Length + 1];
                parameterTypes[0] = (RuntimeType)typeof(State);
                Array.Copy(NormalizedCtorArgs, 0, parameterTypes, 1, NormalizedCtorArgs.Length);

                DynamicMethod dynamicMethod = new DynamicMethod(
                    name: CreateDynamicMethodNameString(),
                    returnType: DeclaringValueType ?? typeof(object),
                    parameterTypes: parameterTypes,
                    owner: parameterTypes[0]);
                dynamicMethod.InitLocals = false; // will set this to true later if needed

                ILGenerator ilGen = dynamicMethod.GetILGenerator();

                // Preamble: get 'state' ("this") into a local
                LocalBuilder stateLocal = ilGen.DeclareLocal(typeof(State));
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Stloc, stateLocal);

                LocalBuilder retValLocal = ilGen.DeclareLocal(DeclaringValueType ?? typeof(object));
                RuntimeType topOfStackBeforeCtorInvoke;

                if (ShuffleType != ShuffleType.ValueTypeCtorReturnedByValue)
                {
                    // Typical case: Creating a class or a boxed valuetype, call the allocator
                    ilGen.Emit(OpCodes.Ldloc, stateLocal);
                    ilGen.Emit(OpCodes.Ldfld, State.fi_pAllocFirstArg);
                    ilGen.Emit(OpCodes.Ldloc, stateLocal);
                    ilGen.Emit(OpCodes.Ldfld, State.fi_pfnAlloc);
                    ilGen.EmitCalli(OpCodes.Calli, CallingConventions.Standard, typeof(object), new[] { typeof(void*) }, null);
                    ilGen.Emit(OpCodes.Stloc, retValLocal);

                    // Need to make sure the type wasn't collected before we called the allocator
                    ilGen.Emit(OpCodes.Ldloc, stateLocal);
                    ilGen.EmitCall(OpCodes.Call, Helpers.mi_gcKeepAlive, null);

                    ilGen.Emit(OpCodes.Ldloc, retValLocal);
                    topOfStackBeforeCtorInvoke = (RuntimeType)typeof(object);

                    if (ShuffleType == ShuffleType.ValueTypeCtorReturnedBoxed)
                    {
                        ilGen.Emit(OpCodes.Ldflda, Box.fi__value);
                        topOfStackBeforeCtorInvoke = (RuntimeType)typeof(byte).MakeByRefType();
                    }
                }
                else
                {
                    // Uncommon case: Returning a valuetype byval
                    dynamicMethod.InitLocals = true; // need to zero out the value type before passing the ref to the ctor
                    ilGen.Emit(OpCodes.Ldloca, retValLocal);
                    topOfStackBeforeCtorInvoke = (RuntimeType)DeclaringValueType!.MakeByRefType();
                }

                // At this point, our evaluation stack contains:
                //   O, if the stub is for a reference type ctor; or
                //   T&, if the stub is for a value type ctor

                RuntimeType[] ctorParams = new RuntimeType[NormalizedCtorArgs.Length + 1];
                ctorParams[0] = topOfStackBeforeCtorInvoke;
                Array.Copy(NormalizedCtorArgs, 0, ctorParams, 1, NormalizedCtorArgs.Length);

                // Now load the rest of the arguments and call the constructor via calli

                for (int i = 0; i < NormalizedCtorArgs.Length; i++)
                {
                    ilGen.Emit(OpCodes.Ldarg, checked((short)(i + 1)));
                }

                ilGen.Emit(OpCodes.Ldloc, stateLocal);
                ilGen.Emit(OpCodes.Ldfld, State.fi_pfnCtor);
                ilGen.EmitCalli(OpCodes.Calli, CallingConvention, typeof(void), ctorParams, null);

                // The evaluation stack should be empty at this point.
                // Now return the fully-hydrated object.

                ilGen.Emit(OpCodes.Ldloc, retValLocal);
                ilGen.Emit(OpCodes.Ret);

                // Now compile everything.
                // We don't use CreateDelegate because we don't want to create a delegate;
                // we just want to get the runtime handle.

                dynamicMethod.GetMethodDescriptor(); // pre-populate some member fields
                IRuntimeMethodInfo? methodHandle = dynamicMethod.m_methodHandle;
                RuntimeHelpers.CompileMethod(methodHandle?.Value ?? RuntimeMethodHandleInternal.EmptyHandle);
                GC.KeepAlive(methodHandle);

                return dynamicMethod.GetMethodDescriptor(); // recompute post-compilation
            }

            private static void EmitLdarg(ILGenerator ilGen, int argNo)
            {
                if (argNo == 0) { ilGen.Emit(OpCodes.Ldarg_0); }
                else if (argNo == 1) { ilGen.Emit(OpCodes.Ldarg_1); }
                else if (argNo == 2) { ilGen.Emit(OpCodes.Ldarg_2); }
                else if (argNo == 3) { ilGen.Emit(OpCodes.Ldarg_3); }
                else if (argNo <= byte.MaxValue) { ilGen.Emit(OpCodes.Ldarg_S, (byte)argNo); }
                else { ilGen.Emit(OpCodes.Ldarg, checked((short)argNo)); }
            }

            public override bool Equals(object? obj) => Equals(obj as CachedStubEntry);

            public bool Equals(CachedStubEntry? obj)
            {
                if (obj is null) return false;
                if (ShuffleType != obj.ShuffleType) return false;
                if (CallingConvention != obj.CallingConvention) return false;
                if (!Equals(DeclaringValueType, obj.DeclaringValueType)) return false;

                if (NormalizedCtorArgs.Length != obj.NormalizedCtorArgs.Length) return false;
                for (int i = 0; i < NormalizedCtorArgs.Length; i++)
                {
                    if (!NormalizedCtorArgs[i].Equals(obj.NormalizedCtorArgs[i])) return false;
                }

                return true;
            }

            public override int GetHashCode()
            {
                HashCode hashCode = default;
                hashCode.Add((int)ShuffleType);
                hashCode.Add((int)CallingConvention);
                hashCode.Add(DeclaringValueType?.GetHashCode() ?? 0);
                for (int i = 0; i < NormalizedCtorArgs.Length; i++)
                {
                    hashCode.Add(NormalizedCtorArgs[i].GetHashCode());
                }
                return hashCode.ToHashCode();
            }

            internal RuntimeMethodHandle GetOrCreateStub()
            {
                lock (this)
                {
                    return _stub ??= CreateStub();
                }
            }
        }

        private static RuntimeType GetNormalizedType(RuntimeType runtimeType)
        {
            return s_normalizedTypes[(int)GetSharedParameterRepresentation(runtimeType)] ?? runtimeType;
        }

        private static SharedParameterRepresentation GetSharedParameterRepresentation(RuntimeType runtimeType)
        {
            CorElementType corElementType = RuntimeTypeHandle.GetCorElementType(runtimeType);
            switch (corElementType)
            {
                case CorElementType.ELEMENT_TYPE_VOID:
                    return SharedParameterRepresentation.Void; // void

                case CorElementType.ELEMENT_TYPE_I1:
                case CorElementType.ELEMENT_TYPE_U1:
                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    return SharedParameterRepresentation.U1; // byte, sbyte, bool

                case CorElementType.ELEMENT_TYPE_I2:
                case CorElementType.ELEMENT_TYPE_U2:
                case CorElementType.ELEMENT_TYPE_CHAR:
                    return SharedParameterRepresentation.U2; // short, ushort, char

                case CorElementType.ELEMENT_TYPE_I4:
                case CorElementType.ELEMENT_TYPE_U4:
                    return SharedParameterRepresentation.U4; // int, uint

                case CorElementType.ELEMENT_TYPE_I8:
                case CorElementType.ELEMENT_TYPE_U8:
                    return SharedParameterRepresentation.U8; // long, ulong

                case CorElementType.ELEMENT_TYPE_I:
                case CorElementType.ELEMENT_TYPE_U:
                case CorElementType.ELEMENT_TYPE_PTR:
                case CorElementType.ELEMENT_TYPE_FNPTR:
                    return Environment.Is64BitProcess ? SharedParameterRepresentation.U8 : SharedParameterRepresentation.U4; // unmanaged pointers go as ints / longs

                case CorElementType.ELEMENT_TYPE_R4:
                    return SharedParameterRepresentation.R4; // float

                case CorElementType.ELEMENT_TYPE_R8:
                    return SharedParameterRepresentation.R8; // double

                case CorElementType.ELEMENT_TYPE_STRING:
                case CorElementType.ELEMENT_TYPE_BYREF:
                case CorElementType.ELEMENT_TYPE_CLASS:
                case CorElementType.ELEMENT_TYPE_OBJECT:
                case CorElementType.ELEMENT_TYPE_ARRAY:
                case CorElementType.ELEMENT_TYPE_SZARRAY:
                    return SharedParameterRepresentation.ByRef; // T& or Object

                case CorElementType.ELEMENT_TYPE_VALUETYPE:
                    if (runtimeType.IsEnum)
                    {
                        return GetSharedParameterRepresentation(Enum.InternalGetUnderlyingType(runtimeType));
                    }
                    goto default;

                default:
                    return SharedParameterRepresentation.Unknown;
            }
        }

        private enum SharedParameterRepresentation
        {
            Unknown = 0, // used for custom non-primitive value types
            Void, // void
            ByRef, // T& and Object
            U1, // byte, sbyte, bool
            U2, // short, ushort, char
            U4, // int, uint
            U8, // long, ulong
            R4, // float
            R8, // double
        }

        private enum ShuffleType
        {
            RefTypeCtor,
            ValueTypeCtorReturnedByValue,
            ValueTypeCtorReturnedBoxed,
        }

        private static class Helpers
        {
            internal static readonly MethodInfo mi_gcKeepAlive = typeof(GC).GetMethod("KeepAlive", BindingFlags.Static | BindingFlags.Public, new Type[] { typeof(object) })!;
        }

        private unsafe sealed class State
        {
            internal static readonly FieldInfo fi_pfnAlloc = typeof(State).GetField(nameof(pfnAlloc))!;
            internal static readonly FieldInfo fi_pfnCtor = typeof(State).GetField(nameof(pfnCtor))!;
            internal static readonly FieldInfo fi_pAllocFirstArg = typeof(State).GetField(nameof(pAllocFirstArg))!;

            // Three fields below are used by our stub method

            public delegate* managed<void*, object> pfnAlloc;
            public IntPtr pfnCtor;
            public void* pAllocFirstArg;

            // Two fields below exist so that GC.KeepAlive(state) also roots critical info

            public RuntimeType? ctorDeclaredType;
            public CachedStubEntry? cachedStubEntry;
        }

        private sealed class Box
        {
            internal static readonly FieldInfo fi__value = typeof(Box).GetField(nameof(_value))!;

            public readonly byte _value;
        }
    }
}
