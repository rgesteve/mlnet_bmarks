// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.CSharp.RuntimeBinder.Tests
{
    public class ArrayHandling
    {
        [ConditionalFact(nameof(PlatformDetection) + "." + nameof(PlatformDetection.IsNonZeroLowerBoundArraySupported))]
        public void SingleRankNonSZArray()
        {
            dynamic d = Array.CreateInstance(typeof(int), new[] { 8 }, new[] { -2 });
            d.SetValue(32, 3);
            d.SetValue(28, -1);
            Assert.Equal(32, d.GetValue(3));
            Assert.Equal(28, d.GetValue(-1));
        }

        [Fact]
        public void ArrayTypeNames()
        {
            dynamic d;
            RuntimeBinderException ex;

            if (PlatformDetection.IsNonZeroLowerBoundArraySupported)
            {
                d = Array.CreateInstance(typeof(int), new[] { 8 }, new[] { -2 });
                ex = Assert.Throws<RuntimeBinderException>(() => { string s = d; });
                if (!PlatformDetection.IsNetNative) // .NET Native toolchain optimizes away Exception messages.
                {
                    Assert.Contains("int[*]", ex.Message);
                }
            }

            d = new int[3];
            ex = Assert.Throws<RuntimeBinderException>(() => { string s = d; });
            if (!PlatformDetection.IsNetNative) // .NET Native toolchain optimizes away Exception messages.
            {
                Assert.Contains("int[]", ex.Message);
            }
        }

        [Fact]
        public void MultiDimArrayTypeNames()
        {
            dynamic d = new int[3, 2, 1];
            RuntimeBinderException ex = Assert.Throws<RuntimeBinderException>(() => { string s = d; });
            if (!PlatformDetection.IsNetNative) // .NET Native toolchain optimizes away Exception messages.
            {
                Assert.Contains("int[,,]", ex.Message);
            }

            if (PlatformDetection.IsNonZeroLowerBoundArraySupported)
            {
                d = Array.CreateInstance(typeof(int), new[] { 3, 2, 1 }, new[] { -2, 2, -0 });
                ex = Assert.Throws<RuntimeBinderException>(() => { string s = d; });
                if (!PlatformDetection.IsNetNative) // .NET Native toolchain optimizes away Exception messages.
                {
                    Assert.Contains("int[,,]", ex.Message);
                }
            }
        }

        [Fact]
        public void IncorrectNumberOfIndices()
        {
            dynamic d = new int[2, 2, 2];
            RuntimeBinderException ex = Assert.Throws<RuntimeBinderException>(() => d[1] = 0);
            if (!PlatformDetection.IsNetNative) // .NET Native toolchain optimizes away Exception messages.
            {
                Assert.Contains("[]", ex.Message);
                Assert.Contains("'3'", ex.Message);
            }


            ex = Assert.Throws<RuntimeBinderException>(() => d[1, 2, 3, 4] = 0);
            if (!PlatformDetection.IsNetNative) // .NET Native toolchain optimizes away Exception messages.
            {
                Assert.Contains("[]", ex.Message);
                Assert.Contains("'3'", ex.Message);
            }

            ex = Assert.Throws<RuntimeBinderException>(() => d[1]);
            if (!PlatformDetection.IsNetNative) // .NET Native toolchain optimizes away Exception messages.
            {
                Assert.Contains("[]", ex.Message);
                Assert.Contains("'3'", ex.Message);
            }

            ex = Assert.Throws<RuntimeBinderException>(() => d[1, 2, 3, 4]);
            if (!PlatformDetection.IsNetNative) // .NET Native toolchain optimizes away Exception messages.
            {
                Assert.Contains("[]", ex.Message);
                Assert.Contains("'3'", ex.Message);
            }

            d = new int[2];
            ex = Assert.Throws<RuntimeBinderException>(() => d[1, 2, 3, 4] = 0);
            if (!PlatformDetection.IsNetNative) // .NET Native toolchain optimizes away Exception messages.
            {
                Assert.Contains("[]", ex.Message);
                Assert.Contains("'1'", ex.Message);
            }

            ex = Assert.Throws<RuntimeBinderException>(() => d[1, 2, 3, 4]);
            if (!PlatformDetection.IsNetNative) // .NET Native toolchain optimizes away Exception messages.
            {
                Assert.Contains("[]", ex.Message);
                Assert.Contains("'1'", ex.Message);
            }
        }
    }
}
