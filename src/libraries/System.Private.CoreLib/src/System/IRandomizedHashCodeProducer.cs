// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System
{
    internal interface IRandomizedHashCodeProducer
    {
        /// <summary>
        /// Produces a randomized hash code for the current object.
        /// </summary>
        /// <returns>
        /// A randomized hash code.
        /// </returns>
        /// <remarks>
        /// (fill me in)
        /// </remarks>
        int GetRandomizedHashCode();
    }
}
