BizTalkCoreCLR
===========================
A benchmark ported from BizTalk to test CoreCLR.
## Build
1. Fork it.
2. `cd path/to/BizTalkCoreCLR`
3. Change `<RuntimeIdentifier>win7-x64</RuntimeIdentifier>` in file PerfMark.csproj to `<RuntimeIdentifier>ubuntu.14.04-x64</RuntimeIdentifier>` if your platform is Ubuntu.
4. Delete `<ServerGarbageCollection>true</ServerGarbageCollection>` in file PerfMark.csproj if you do not use server GC.
5. `dotnet restore`
6. `dotnet publish -c Release`
## Run
1. Copy **BizTalk.dll** and **biztalk.xsl** from **path/to/BizTalkCoreCLR/Biztalk** folder to **path/to/BizTalkCoreCLR/bin/Release/netcoreapp2.0/win7-x64/publish** folder.
2. Copy your CoreCLR bits to **path/to/BizTalkCoreCLR/bin/Release/netcoreapp2.0/win7-x64/publish** folder.
3. `cd path/to/BizTalkCoreCLR/bin/Release/netcoreapp2.0/win7-x64/publish`
4. `dotnet PerfMark.dll -a BizTalk.dll -c PerfTest.BizTalk.Biztalk -tp 100 -w 20 -d 60 -m Run`
## History
First version was created on 6/14/2017 by helloguo.
## License
**Intel Confidential**. This is an internal benchmark for Intel SSG WOS Tools and Runtimes team.
TODO: Add license.
