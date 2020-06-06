// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.InteropServices
{
    internal sealed partial class ShroudedBufferHandle
    {
        internal IntPtr AllocateCore()
            => Marshal.AllocHGlobal((nint)_cbData);

        private bool ReleaseHandleCore()
        {
            Marshal.FreeHGlobal(handle);
            return true;
        }
    }
}
