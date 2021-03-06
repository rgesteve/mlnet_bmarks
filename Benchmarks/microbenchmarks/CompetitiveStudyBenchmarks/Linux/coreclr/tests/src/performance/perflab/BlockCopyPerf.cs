// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Xunit.Performance;
using System;
using Xunit;

namespace PerfLabTests
{
    public class BlockCopyPerf
    {
        [Benchmark(InnerIterationCount = 1000000)]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static void CallBlockCopy(int numElements)
        {
            byte[] bytes = new byte[numElements * 2];
            Buffer.BlockCopy(bytes, 0, bytes, numElements, numElements);

            foreach (var iteration in Benchmark.Iterations)
                using (iteration.StartMeasurement())
                    for (int i = 0; i < Benchmark.InnerIterationCount; i++)
                        Buffer.BlockCopy(bytes, 0, bytes, numElements, numElements);
        }
    }
}
