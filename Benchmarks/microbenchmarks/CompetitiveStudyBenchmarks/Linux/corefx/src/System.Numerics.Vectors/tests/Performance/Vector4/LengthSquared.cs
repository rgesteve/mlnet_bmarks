// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Xunit.Performance;

namespace System.Numerics.Tests
{
    public static partial class Perf_Vector4
    {
        [Benchmark(InnerIterationCount = VectorTests.DefaultInnerIterationsCount)]
        public static void LengthSquaredBenchmark()
        {
            const float expectedResult = 4.0f;

            foreach (var iteration in Benchmark.Iterations)
            {
                float actualResult;

                using (iteration.StartMeasurement())
                {
                    actualResult = LengthSquaredTest();
                }

                VectorTests.AssertEqual(expectedResult, actualResult);
            }
        }

        public static float LengthSquaredTest()
        {
            var result = 0.0f;

            for (var iteration = 0; iteration < Benchmark.InnerIterationCount; iteration++)
            {
                // The inputs aren't being changed and the output is being reset with each iteration, so a future
                // optimization could potentially throw away everything except for the final call. This would break
                // the perf test. The JitOptimizeCanary code below does modify the inputs and consume each output.
                result = VectorTests.Vector4Value.LengthSquared();
            }

            return result;
        }

        [Benchmark(InnerIterationCount = VectorTests.DefaultInnerIterationsCount)]
        public static void LengthSquaredJitOptimizeCanaryBenchmark()
        {
            const float expectedResult = 67108864.0f;

            foreach (var iteration in Benchmark.Iterations)
            {
                float actualResult;

                using (iteration.StartMeasurement())
                {
                    actualResult = LengthSquaredJitOptimizeCanaryTest();
                }

                VectorTests.AssertEqual(expectedResult, actualResult);
            }
        }

        public static float LengthSquaredJitOptimizeCanaryTest()
        {
            var result = 0.0f;
            var value = VectorTests.Vector4Value;

            for (var iteration = 0; iteration < Benchmark.InnerIterationCount; iteration++)
            {
                value += VectorTests.Vector4Delta;
                result += value.LengthSquared();
            }

            return result;
        }
    }
}
