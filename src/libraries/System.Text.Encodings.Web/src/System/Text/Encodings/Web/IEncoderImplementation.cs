// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace System.Text.Encodings.Web
{
    internal interface IEncoderImplementation
    {
        int MaxOutputCharsPerInputRune { get; }

        int EncodeToBuffer(Rune rune, Span<char> buffer);

        bool TryEncodeToBuffer(Rune rune, Span<char> buffer, out int charsWritten);
    }
}
