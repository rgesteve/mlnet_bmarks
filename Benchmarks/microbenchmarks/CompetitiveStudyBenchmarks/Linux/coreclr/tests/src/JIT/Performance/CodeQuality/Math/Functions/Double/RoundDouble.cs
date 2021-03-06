// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Xunit.Performance;

namespace Functions
{
    public static partial class MathTests
    {
        // Tests Math.Round(double) over 5000 iterations for the domain -PI/2, +PI/2

        private const double roundDoubleDelta = 0.0006283185307180;
        private const double roundDoubleExpectedResult = 2;

        [Benchmark]
        public static void RoundDoubleBenchmark()
        {
            foreach (var iteration in Benchmark.Iterations)
            {
                using (iteration.StartMeasurement())
                {
                    RoundDoubleTest();
                }
            }
        }

        public static void RoundDoubleTest()
        {
            var result = 0.0; var value = -1.5707963267948966;

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                value += roundDoubleDelta;
                result += Math.Round(value);
            }

            var diff = Math.Abs(roundDoubleExpectedResult - result);

            if (diff > doubleEpsilon)
            {
                throw new Exception($"Expected Result {roundDoubleExpectedResult}; Actual Result {result}");
            }
        }
    }
}
