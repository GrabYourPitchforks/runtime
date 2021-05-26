// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace System
{
    // [StackTraceHidden]
    internal static class ThrowHelper
    {
        [DoesNotReturn]
        internal static void ThrowArgumentException_CannotExtractScalar(ExceptionArgument argument)
        {
            throw new ArgumentException(SR.Argument_CannotExtractScalar, GetArgumentName(argument));
        }

        [DoesNotReturn]
        internal static void ThrowArgumentException_DestinationTooShort()
        {
            throw new ArgumentException(SR.Argument_DestinationTooShort, "destination");
        }

        [DoesNotReturn]
        internal static void ThrowArgumentNullException(ExceptionArgument argument)
        {
            throw new ArgumentNullException(GetArgumentName(argument));
        }

        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument)
        {
            throw new ArgumentOutOfRangeException(GetArgumentName(argument));
        }

        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRange_IndexException()
            => ThrowArgumentOutOfRangeException(ExceptionArgument.index);

        private static string GetArgumentName(ExceptionArgument argument)
        {
            return argument.ToString();
        }
    }

    internal enum ExceptionArgument
    {
        ch,
        culture,
        index,
        input,
        value,
    }
}
