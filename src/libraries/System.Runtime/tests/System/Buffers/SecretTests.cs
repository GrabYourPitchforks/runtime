// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace System.Buffers.Tests
{
    public static class SecretTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Hello world!")]
        public static void CtorFromSpan_ThenGetLength(string value)
        {
            ReadOnlySpan<char> valueAsSpan = value.AsSpan();
            Secret<char> secret = new Secret<char>(valueAsSpan);
            Assert.Equal(valueAsSpan.Length, secret.GetLength()); // length measured in elements, not bytes
        }

        [Theory]
        [InlineData("")]
        [InlineData("Hello world!")]
        public static void CreateFromString_ThenRevealToString(string value)
        {
            Secret<char> secret = Secret.Create(value);
            Assert.Equal(value, secret.RevealToString());
        }

        [Fact]
        public static void CreateFromString_NullArg_Throws()
        {
            Assert.Throws<ArgumentNullException>("value", () => Secret.Create((string)null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Hello world!")]
        public static void CreateFromBuffer_ThenRevealToArray(string value)
        {
            Secret<char> secret = Secret.Create(value.ToCharArray());
            Assert.Equal(value.ToCharArray(), secret.RevealToArray());
        }

        [Fact]
        public static void CreateFromBuffer_NullArg_Throws()
        {
            Assert.Throws<ArgumentNullException>("buffer", () => Secret.Create((char[])null));
        }

        [Fact]
        public static void RevealToString_NullArg_Throws()
        {
            Assert.Throws<ArgumentNullException>("secret", () => Secret.RevealToString(null));
        }

        [Fact]
        public static void Clone_Success()
        {
            Secret<double> original = new Secret<double>(stackalloc double[] { 1, 2, 3 });
            Secret<double> clone = original.Clone();
            original.Dispose(); // should not dispose of clone
            Assert.Equal(new double[] { 1, 2, 3 }, clone.RevealToArray());
        }

        [Fact]
        public static void GetLength_FailsAfterDispose()
        {
            Secret<char> secret = new Secret<char>("Hello!");
            secret.Dispose();
            Assert.Throws<ObjectDisposedException>(() => secret.GetLength());
        }

        [Fact]
        public static void RevealIntoSpan_Success()
        {
            float[] destination = new float[] { 0, 0, 0, 0, 0 };

            // With span properly sized.
            Secret<float> secret = new Secret<float>(stackalloc float[] { 1, 2, 3, 4, 5 });
            int elementsWritten = secret.RevealInto(destination);
            Assert.Equal(5, elementsWritten);
            Assert.Equal(new float[] { 1, 2, 3, 4, 5 }, destination);

            // With span oversized, should still succeed.
            secret = new Secret<float>(stackalloc float[] { 10, 20, 30 });
            elementsWritten = secret.RevealInto(destination);
            Assert.Equal(3, elementsWritten);
            Assert.Equal(new float[] { 10, 20, 30, 4, 5 }, destination);
        }

        [Fact]
        public static void RevealIntoSpan_Failure()
        {
            float[] destination = new float[] { 0, 0, 0, 0, 0 };

            // With span undersized.
            Secret<float> secret = new Secret<float>(stackalloc float[] { 1, 2, 3, 4, 5, 6 });
            Assert.Throws<ArgumentException>("destination", () => secret.RevealInto(destination));
            Assert.Equal(new float[] { 0, 0, 0, 0, 0 }, destination); // should not have mutated buffer
        }

        [Fact]
        public static void RevealToArray()
        {
            Secret<double> secret = new Secret<double>(stackalloc double[] { 1, -1, 2, -2 });
            double[] result = secret.RevealToArray();
            Assert.Equal(new double[] { 1, -1, 2, -2 }, result);
        }

        [Fact]
        public static void RevealAndUse_ActionIsNull_Throws()
        {
            Secret<double> secret = new Secret<double>(stackalloc double[] { 1, -1, 2, -2 });
            Assert.Throws<ArgumentNullException>("spanAction",
                () => secret.RevealAndUse(0, null));
        }

        [Fact]
        public static void RevealAndUse_DoesNotPointToRawData()
        {
            // n.b. It is illegal to attempt to mutate the contents of the span provided to the callback.
            // In the real world this would result in undefined behavior. For testing purposes, we're only
            // using this to validate that we receive a span which represents a copy of the secret data,
            // not the secret data itself. This could be because the secret data is not available in the
            // current process in unobfuscated form, because the secret data is not properly aligned for
            // creating a ReadOnlySpan<T> directly over it, or any other reason.

            Secret<double> secret = new Secret<double>(stackalloc double[] { 1, 2, 3 });
            secret.RevealAndUse(0, static (span, _) =>
            {
                Span<double> mutableSpan = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), span.Length);
                Assert.Equal(new double[] { 1, 2, 3 }, mutableSpan.ToArray());
                mutableSpan.Clear();
            });
            Assert.Equal(new double[] { 1, 2, 3 }, secret.RevealToArray()); // validate data was not mutated
        }

        [Fact]
        public static void RevealAndUse_SmallData_Success()
        {
            Secret<char> secret = new Secret<char>("Hello world!");
            StringBuilder builder = new StringBuilder();
            secret.RevealAndUse(builder, static (span, builder) => builder.Append(span));
            Assert.Equal("Hello world!", builder.ToString());
        }

        [Fact]
        public static void RevealAndUse_ImplFallsBackToHeapForLargeData_Success()
        {
            byte[] secretData = new byte[64 * 1024]; // will bypass stackalloc logic in RevealAndUse
            Random.Shared.NextBytes(secretData);

            bool wasCalled = false;
            Secret<byte> secret = new Secret<byte>(secretData);
            secret.RevealAndUse(secretData, (span, expectedSecretData) =>
            {
                Assert.Equal(expectedSecretData, span.ToArray());
                wasCalled = true;
            });
            Assert.True(wasCalled);
        }

        [Fact]
        public static void ToString_IsJustTypeName()
        {
            // If the implementation of object.ToString ever changes, this test will fail.
            // That's fine, we can always update this test if needed. Our ToString method
            // doesn't make any guarantees beyond that it won't disclose the secret data length
            // or the secret data contents.

            object secret = new Secret<char>("Hello world!"); // go through object.ToString to avoid obsoletion
            Assert.Equal(secret.GetType().ToString(), secret.ToString());
        }

        // Tests with data > 2GB in size, which exercises that our code paths properly use nuint
        // instead of simply int32 everywhere.
        // NOTE: This test is constrained to run on Windows and MacOSX because it causes
        //       problems on Linux due to the way deferred memory allocation works. On Linux, the allocation can
        //       succeed even if there is not enough memory but then the test may get killed by the OOM killer at the
        //       time the memory is accessed which triggers the full memory allocation.
        // [OuterLoop]
        [PlatformSpecific(TestPlatforms.Windows | TestPlatforms.OSX)]
        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.Is64BitProcess))]
        public unsafe static void BigDataTests()
        {
            int[] ints = GC.AllocateUninitializedArray<int>(1_500_000_000);
            ints.AsSpan().Fill(0x12345678);
            Secret<int> secret = new Secret<int>(ints);

            // GetLength

            Assert.Equal(ints.Length, secret.GetLength());

            // RevealInto(Span)

            int[] ints2 = GC.AllocateUninitializedArray<int>(ints.Length + 1);
            Assert.Equal(ints.Length, secret.RevealInto(ints2));
            Assert.True(ints.AsSpan().SequenceEqual(ints2.AsSpan(0, ints.Length)));
            ints2 = null;

            // RevealTo(Array)

            int[] ints3 = secret.RevealToArray();
            Assert.True(ints.AsSpan().SequenceEqual(ints3.AsSpan()));
            ints3 = null;

            // RevealAndUse

            bool revealAndUseSucceeded = false;
            secret.RevealAndUse(ints, (span, expectedInts) =>
            {
                revealAndUseSucceeded = expectedInts.AsSpan().SequenceEqual(span);
            });
            Assert.True(revealAndUseSucceeded);

            // Clone

            Secret<int> clone = secret.Clone();
            secret.Dispose();
            bool cloneSucceeded = false;
            clone.RevealAndUse(ints, (span, expectedInts) =>
            {
                cloneSucceeded = expectedInts.AsSpan().SequenceEqual(span);
            });
            Assert.True(cloneSucceeded);
            clone.Dispose();
            ints = null;

            // We don't test RevealToString here because strings are limited to approx. 1bn elements.
            // See AllocateString in gchelpers.cpp.
        }
    }
}
