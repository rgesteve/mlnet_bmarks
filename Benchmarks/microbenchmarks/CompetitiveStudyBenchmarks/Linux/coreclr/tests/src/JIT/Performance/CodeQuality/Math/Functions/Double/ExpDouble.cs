// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Xunit.Performance;

namespace Functions
{
    public static partial class MathTests
    {
        // Tests Math.Exp(double) over 5000 iterations for the domain -1, +1

        private const double expDoubleDelta = 0.0004;
        private const double expDoubleExpectedResult = 5877.1812477590884;

        [Benchmark]
        public static void ExpDoubleBenchmark()
        {
            foreach (var iteration in Benchmark.Iterations)
            {
                using (iteration.StartMeasurement())
                {
                    ExpDoubleTest();
                }
            }
        }

        public static void ExpDoubleTest()
        {
            var result = 0.0; var value = -1.0;

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                value += expDoubleDelta;
                result += Math.Exp(value);
            }

            var diff = Math.Abs(expDoubleExpectedResult - result);

            if (diff > doubleEpsilon)
            {
                throw new Exception($"Expected Result {expDoubleExpectedResult}; Actual Result {result}");
            }
        }
    }
}
