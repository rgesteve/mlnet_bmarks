// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Xunit.Performance;

namespace Functions
{
    public static partial class MathTests
    {
        // Tests Math.Acos(double) over 5000 iterations for the domain -1, +1

        private const double acosDoubleDelta = 0.0004;
        private const double acosDoubleExpectedResult = 7852.4108380716079;

        [Benchmark]
        public static void AcosDoubleBenchmark()
        {
            foreach (var iteration in Benchmark.Iterations)
            {
                using (iteration.StartMeasurement())
                {
                    AcosDoubleTest();
                }
            }
        }

        public static void AcosDoubleTest()
        {
            var result = 0.0; var value = -1.0;

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                value += acosDoubleDelta;
                result += Math.Acos(value);
            }

            var diff = Math.Abs(acosDoubleExpectedResult - result);

            if (diff > doubleEpsilon)
            {
                throw new Exception($"Expected Result {acosDoubleExpectedResult}; Actual Result {result}");
            }
        }
    }
}
