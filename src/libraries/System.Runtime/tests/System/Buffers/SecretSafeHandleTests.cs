// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace System.Buffers.Tests
{
    public unsafe static class SecretSafeHandleTests
    {
        private static readonly Type SecretSafeHandleType = typeof(Secret).Assembly.GetType("System.Buffers.SecretSafeHandle");
        private delegate void DangerousGetRawDataDel(out nuint byteCount, out void* pData);

        [Fact]
        public static void Allocate_OutOfMemory()
        {
            Worker(-1);
            Worker(-IntPtr.Size);
            Worker(-IntPtr.Size - 1);

            static void Worker(nint byteCount)
            {
                Assert.Throws<OutOfMemoryException>(
                    () => SecretSafeHandleType.GetMethod("Allocate").Invoke(null, BindingFlags.DoNotWrapExceptions, null, new object[] { (nuint)byteCount }, null));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1024 * 1024)]
        public static void AllocateAndFill(int expectedByteCount)
        {
            using SafeHandle handle = (SafeHandle)SecretSafeHandleType.GetMethod("Allocate").Invoke(null, new object[] { (nuint)expectedByteCount });
            DangerousGetRawData(handle, out nuint actualByteCount, out void* pData);

            // Ensure the output values make sense.
            Assert.Equal((nuint)expectedByteCount, actualByteCount);
            Assert.True(pData != null);

            // Ensure the entire span is writable without AVing.
            new Span<byte>(pData, expectedByteCount).Clear();

            // Ideally we'd also be able to test that the data is cleared
            // on dispose; but since this results in a call to free / HeapFree,
            // we can't test this without risking destabilizing the test runner.
        }

        [Fact]
        public static void Duplicate()
        {
            const nuint byteCount = 1024;
            using SafeHandle originalHandle = (SafeHandle)SecretSafeHandleType.GetMethod("Allocate").Invoke(null, new object[] { byteCount });

            // Fill the original handle with data, then duplicate it.
            DangerousGetRawData(originalHandle, out nuint originalByteLength, out void* pOriginal);
            Assert.Equal(byteCount, originalByteLength);
            new Span<byte>(pOriginal, (int)byteCount).Fill((byte)'a');
            using SafeHandle duplicateHandle = (SafeHandle)SecretSafeHandleType.GetMethod("Duplicate").Invoke(originalHandle, null);

            // Ensure the original and duplicate handle point to different addresses.
            DangerousGetRawData(duplicateHandle, out nuint duplicateByteLength, out void* pDuplicate);
            Assert.Equal(byteCount, duplicateByteLength);
            Span<byte> duplicateSpan = new Span<byte>(pDuplicate, (int)byteCount);
            Assert.NotEqual((IntPtr)pOriginal, (IntPtr)pDuplicate);

            // Fill the original with new data; ensure no part of the duplicate is overwritten.
            new Span<byte>(pOriginal, (int)byteCount).Fill((byte)'b');
            Assert.Equal(Enumerable.Repeat((byte)'a', (int)byteCount).ToArray(), duplicateSpan.ToArray());
        }

        [Fact]
        public static void ReflectionDoesNotDiscloseLengthOrDataPointer()
        {
            const nuint byteCount = 102983; // random value unlikely to ever surface
            using SafeHandle handle = (SafeHandle)SecretSafeHandleType.GetMethod("Allocate").Invoke(null, new object[] { byteCount });

            // Build the list of values coming from reflection.
            // Converting to strings is more straightforward than trying to special-case every field type which might exist.
            StringBuilder sb = new StringBuilder();
            FieldInfo[] allFields = handle.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            foreach (FieldInfo fieldInfo in allFields)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0}{1}", fieldInfo.GetValue(handle), Environment.NewLine);
            }
            PropertyInfo[] allProps = handle.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            foreach (PropertyInfo propInfo in allProps)
            {
                // If there are any properties that don't have parameterless getters, we'll need to update the logic below not to throw.
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0}{1}", propInfo.GetValue(handle), Environment.NewLine);
            }

            // Build the list of values which should not be present.
            DangerousGetRawData(handle, out _, out void* pData);
            string[] forbiddenValues = new string[]
            {
                byteCount.ToString(CultureInfo.InvariantCulture), // length (as decimal)
                ((ulong)pData).ToString(CultureInfo.InvariantCulture), // pointer to data (as decimal)
                ((ulong)pData).ToString("x", CultureInfo.InvariantCulture), // pointer to data (as hex)
                ((ulong)pData - (uint)sizeof(IntPtr)).ToString(CultureInfo.InvariantCulture), // pointer to header (as decimal)
                ((ulong)pData - (uint)sizeof(IntPtr)).ToString("x", CultureInfo.InvariantCulture), // pointer to header (as hex)
            };

            foreach (string forbiddenValue in forbiddenValues)
            {
                Assert.DoesNotContain(forbiddenValue, sb.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public static void ShroudedPointerHasNoUsefulReflectableMembers()
        {
            Type shroudedPointerType = typeof(Secret).Assembly.GetType("System.Runtime.InteropServices.ShroudedPointer");
            Assert.NotNull(shroudedPointerType);
            Assert.True(shroudedPointerType.IsValueType);

            // If no fields or properties, then there's nothing for reflection to inspect which could leak the data.

            FieldInfo[] allFields = shroudedPointerType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.Empty(allFields);

            PropertyInfo[] allProperties = shroudedPointerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.Empty(allProperties);

            // We'll also ensure there are no callable methods.

            object shroudedPointerInstance = RuntimeHelpers.GetUninitializedObject(shroudedPointerType);
            MethodInfo[] allMethods = shroudedPointerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (MethodInfo mi in allMethods)
            {
                if (mi.GetParameters().Length == 0)
                {
                    Assert.ThrowsAny<Exception>(() => mi.Invoke(shroudedPointerInstance, BindingFlags.DoNotWrapExceptions, null, null, null));
                }
            }
        }

        private static void DangerousGetRawData(SafeHandle handle, out nuint byteCount, out void* pData)
        {
            var mi = handle.GetType().GetMethod("DangerousGetRawData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mi);

            var del = mi.CreateDelegate<DangerousGetRawDataDel>(handle);
            del(out byteCount, out pData);
        }
    }
}
