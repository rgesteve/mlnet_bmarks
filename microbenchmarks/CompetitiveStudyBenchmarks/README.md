README - Build/Run single-threaded .NET Core benchmarks  
=======================================================

This README lists the steps to build and run single-threaded .NET Core workloads for competitive analysis. The following benchmarks were shortlisted and mainly involve math calculations - ByteMark-Int, ByteMark-FP, SciMark, RayTracer, Adams-Moulton Predictor Corrector, Burgers & Roslyn. Timing blocks were added and duration of run was extended where appropriate. 

Build workloads - Windows
=========================

-   Install pre-requisites to build CoreCLR from https://github.com/dotnet/coreclr/blob/master/Documentation/building/windows-instructions.md.

-   Do "cd <CompetitiveBenchmarks_dir>\Windows\coreclr" followed by "build.cmd x64 Release"

Run workloads - Windows
=========================

-   Do "set CORE_ROOT=<CompetitiveBenchmarks_dir>\Windows\coreclr\bin\tests\Windows_NT.x64.Release\Tests\Core_Root"

-   Do "cd <coreclr_dir>\bin\tests\Windows_NT.x64.Release\JIT\Performance\CodeQuality". (***Note***: Replace <coreclr_dir> with <CompetitiveBenchmarks_dir>\Windows\coreclr)

-   For ***ByteMark-Int & ByteMark-FP***: Do "<coreclr_dir>\bin\tests\Windows_NT.x64.Release\Tests\Core_Root\CoreRun.exe Bytemark\Bytemark\Bytemark.exe"  

-   For ***SciMark***: Do "<coreclr_dir>\bin\tests\Windows_NT.x64.Release\Tests\Core_Root\CoreRun.exe SciMark\SciMark\SciMark.exe"

-   For ***RayTracer***: Do "<coreclr_dir>\bin\tests\Windows_NT.x64.Release\Tests\Core_Root\CoreRun.exe SIMD\RayTracer\RayTracer\RayTracer.exe"

-   For ***Adams-Moulton***: Do "<coreclr_dir>\bin\tests\Windows_NT.x64.Release\Tests\Core_Root\CoreRun.exe BenchF\Adams\Adams\Adams.exe"

-   For ***Burgers***: Do "<coreclr_dir>\bin\tests\Windows_NT.x64.Release\Tests\Core_Root\CoreRun.exe Burgers\Burgers\Burgers.exe"

-   For ***Roslyn***: Do "<coreclr_dir>\bin\tests\Windows_NT.x64.Release\Tests\Core_Root\CoreRun.exe Roslyn\CscBench\CscBench.exe"

Build workloads - Linux
========================

-   Install pre-requisites to build CoreCLR from https://github.com/dotnet/coreclr/blob/master/Documentation/building/linux-instructions.md.

-   Do "cd <CompetitiveBenchmarks_dir>/Linux/coreclr" followed by "./build.sh x64 Release"

-   Install pre-requisites to build CoreFX from https://github.com/dotnet/corefx/blob/master/Documentation/building/unix-instructions.md.

-   Do "cd <CompetitiveBenchmarks_dir>/Linux/corefx" followed by "./build.sh /p:ConfigurationGroup=Release"

-   Do "cd <CompetitiveBenchmarks_dir>/Linux" followed by "mkdir test"

-   The benchmark binaries need to be built on Windows and copied over to the "test" directory in Linux. Boot into Windows and follow the "Build workloads - Windows" and "Run workloads - Windows" sections to build the binaries. Then, copy the contents of the "<CompetitiveBenchmarks_dir>\Windows\coreclr\bin\tests\Windows_NT.x64.Release\" folder from Windows to the "<CompetitiveBenchmarks_dir>/Linux/test" folder on Linux.   

Run workloads - Linux
=========================

-   Do "export CORE_ROOT=<CompetitiveBenchmarks_dir>/Linux/test/Tests/Core_Root"

-   For ***ByteMark-Int & ByteMark-FP***: Do "<CompetitiveBenchmarks_dir>/Linux/coreclr/tests/runtest.sh --testRootDir=<CompetitiveBenchmarks_dir>/Linux/test --testNativeBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/obj/Linux.x64.Release/tests --coreClrBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --mscorlibDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --coreFxBinDir=<CompetitiveBenchmarks_dir>/Linux/corefx/bin/runtime/netcoreapp-Linux-Release-x64  --testDir=<CompetitiveBenchmarks_dir>/Linux/test/JIT/Performance/CodeQuality/Bytemark/Bytemark -v"  

***Note***: After running any of the benchmarks once with all the flags above, a "coreoverlay" directory would be created at "<CompetitiveBenchmarks_dir>/Linux/test/Tests/coreoverlay". Benchmark runs can then be simplified to "<CompetitiveBenchmarks_dir>/Linux/coreclr/tests/runtest.sh --testRootDir=<CompetitiveBenchmarks_dir>/Linux/test --testNativeBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/obj/Linux.x64.Release/tests --coreOverlayDir=<CompetitiveBenchmarks_dir>/Linux/test/Tests/coreoverlay/  --testDir=<CompetitiveBenchmarks_dir>/Linux/test/JIT/Performance/CodeQuality/Bytemark/Bytemark -v". This can be done for any of the below tests.   

-   For ***SciMark***: Do "<CompetitiveBenchmarks_dir>/Linux/coreclr/tests/runtest.sh --testRootDir=<CompetitiveBenchmarks_dir>/Linux/test --testNativeBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/obj/Linux.x64.Release/tests --coreClrBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --mscorlibDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --coreFxBinDir=<CompetitiveBenchmarks_dir>/Linux/corefx/bin/runtime/netcoreapp-Linux-Release-x64  --testDir=<CompetitiveBenchmarks_dir>/Linux/test/JIT/Performance/CodeQuality/SciMark/SciMark -v"

-   For ***RayTracer***: Do "<CompetitiveBenchmarks_dir>/Linux/coreclr/tests/runtest.sh --testRootDir=<CompetitiveBenchmarks_dir>/Linux/test --testNativeBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/obj/Linux.x64.Release/tests --coreClrBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --mscorlibDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --coreFxBinDir=<CompetitiveBenchmarks_dir>/Linux/corefx/bin/runtime/netcoreapp-Linux-Release-x64  --testDir=<CompetitiveBenchmarks_dir>/Linux/test/JIT/Performance/CodeQuality/SIMD/RayTracer/RayTracer -v"

-   For ***Adams-Moulton***: Do "<CompetitiveBenchmarks_dir>/Linux/coreclr/tests/runtest.sh --testRootDir=<CompetitiveBenchmarks_dir>/Linux/test --testNativeBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/obj/Linux.x64.Release/tests --coreClrBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --mscorlibDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --coreFxBinDir=<CompetitiveBenchmarks_dir>/Linux/corefx/bin/runtime/netcoreapp-Linux-Release-x64  --testDir=<CompetitiveBenchmarks_dir>/Linux/test/JIT/Performance/CodeQuality/BenchF/Adams/Adams -v"

-   For ***Burgers***: Do "<CompetitiveBenchmarks_dir>/Linux/coreclr/tests/runtest.sh --testRootDir=<CompetitiveBenchmarks_dir>/Linux/test --testNativeBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/obj/Linux.x64.Release/tests --coreClrBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --mscorlibDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --coreFxBinDir=<CompetitiveBenchmarks_dir>/Linux/corefx/bin/runtime/netcoreapp-Linux-Release-x64  --testDir=<CompetitiveBenchmarks_dir>/Linux/test/JIT/Performance/CodeQuality/Burgers/Burgers -v"

-   For ***Roslyn***: Do "<CompetitiveBenchmarks_dir>/Linux/coreclr/tests/runtest.sh --testRootDir=<CompetitiveBenchmarks_dir>/Linux/test --testNativeBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/obj/Linux.x64.Release/tests --coreClrBinDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --mscorlibDir=<CompetitiveBenchmarks_dir>/Linux/coreclr/bin/Product/Linux.x64.Release --coreFxBinDir=<CompetitiveBenchmarks_dir>/Linux/corefx/bin/runtime/netcoreapp-Linux-Release-x64  --testDir=<CompetitiveBenchmarks_dir>/Linux/test/JIT/Performance/CodeQuality/Roslyn/CscBench -v"
