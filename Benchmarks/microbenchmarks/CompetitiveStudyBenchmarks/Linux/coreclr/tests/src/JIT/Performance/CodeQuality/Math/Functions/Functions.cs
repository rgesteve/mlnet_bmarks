// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xunit.Performance;

[assembly: OptimizeForBenchmarks]
[assembly: MeasureInstructionsRetired]

namespace Functions
{
    public static class Program
    {
#if DEBUG
        private const int defaultIterations = 1;
#else
        private const int defaultIterations = 1000;
#endif

        private static readonly IDictionary<string, Action> TestList = new Dictionary<string, Action>() {
            ["absdouble"] = MathTests.AbsDoubleTest,
            ["abssingle"] = MathTests.AbsSingleTest,
            ["acosdouble"] = MathTests.AcosDoubleTest,
            ["asindouble"] = MathTests.AsinDoubleTest,
            ["atandouble"] = MathTests.AtanDoubleTest,
            ["atan2double"] = MathTests.Atan2DoubleTest,
            ["ceilingdouble"] = MathTests.CeilingDoubleTest,
            ["cosdouble"] = MathTests.CosDoubleTest,
            ["coshdouble"] = MathTests.CoshDoubleTest,
            ["expdouble"] = MathTests.ExpDoubleTest,
            ["floordouble"] = MathTests.FloorDoubleTest,
            ["logdouble"] = MathTests.LogDoubleTest,
            ["log10double"] = MathTests.Log10DoubleTest,
            ["powdouble"] = MathTests.PowDoubleTest,
            ["rounddouble"] = MathTests.RoundDoubleTest,
            ["sindouble"] = MathTests.SinDoubleTest,
            ["sinhdouble"] = MathTests.SinhDoubleTest,
            ["sqrtdouble"] = MathTests.SqrtDoubleTest,
            ["tandouble"] = MathTests.TanDoubleTest,
            ["tanhdouble"] = MathTests.TanhDoubleTest
        };

        private static int Main(string[] args)
        {
            var isPassing = true; var iterations = defaultIterations;
            ICollection<string> testsToRun = new HashSet<string>();

            try
            {
                for (int index = 0; index < args.Length; index++)
                {
                    if (args[index].ToLowerInvariant() == "-bench")
                    {
                        index++;

                        if ((index >= args.Length) || !int.TryParse(args[index], out iterations))
                        {
                            iterations = defaultIterations;
                        }
                    }
                    else if (args[index].ToLowerInvariant() == "all")
                    {
                        testsToRun = TestList.Keys;
                        break;
                    }
                    else
                    {
                        var testName = args[index].ToLowerInvariant();

                        if (!TestList.ContainsKey(testName))
                        {
                            PrintUsage();
                            break;
                        }

                        testsToRun.Add(testName);
                    }
                }

                if (testsToRun.Count == 0)
                {
                    testsToRun = TestList.Keys;
                }

                foreach (var testToRun in testsToRun)
                {
                    Console.WriteLine($"Running {testToRun} test...");
                    Test(iterations, TestList[testToRun]);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"    Error: {exception.Message}");
                isPassing = false;
            }

            return isPassing ? 100 : -1;
        }

        private static void PrintUsage()
        {
            Console.WriteLine(@"Usage:
Functions [name] [-bench #]

  [name]: The name of the function to test. Defaults to 'all'.
    all");

            foreach (var testName in TestList.Keys)
            {
                Console.WriteLine($"  {testName}");
            }

            Console.WriteLine($@"
  [-bench #]: The number of iterations. Defaults to {defaultIterations}");
        }

        private static void Test(int iterations, Action action)
        {
            // ****************************************************************

            Console.WriteLine("  Warming up...");

            var startTimestamp = Stopwatch.GetTimestamp();

            action();

            var totalElapsedTime = (Stopwatch.GetTimestamp() - startTimestamp);
            var totalElapsedTimeInSeconds = (totalElapsedTime / (double)(Stopwatch.Frequency));

            Console.WriteLine($"    Total Time: {totalElapsedTimeInSeconds}");

            // ****************************************************************

            Console.WriteLine($"  Executing {iterations} iterations...");

            totalElapsedTime = 0L;

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                startTimestamp = Stopwatch.GetTimestamp();

                action();

                totalElapsedTime += (Stopwatch.GetTimestamp() - startTimestamp);
            }

            totalElapsedTimeInSeconds = (totalElapsedTime / (double)(Stopwatch.Frequency));

            Console.WriteLine($"    Total Time: {totalElapsedTimeInSeconds} seconds");
            Console.WriteLine($"    Average Time: {totalElapsedTimeInSeconds / iterations} seconds");

            // ****************************************************************
        }
    }
}
