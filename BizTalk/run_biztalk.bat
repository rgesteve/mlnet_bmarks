dotnet restore
dotnet publish -c Release
xcopy .\BizTalk\BizTalk.dll .\bin\Release\netcoreapp2.0\win7-x64\publish
xcopy .\BizTalk\biztalk.xsl .\bin\Release\netcoreapp2.0\win7-x64\publish
cd .\bin\Release\netcoreapp2.0\win7-x64\publish
timeout /t 125
dotnet  PerfMark.dll -a BizTalk.dll -c PerfTest.BizTalk.Biztalk -tp 100 -w 20 -d 60 -m Run > ../../../../../%1_1.txt
timeout /t 125
dotnet  PerfMark.dll -a BizTalk.dll -c PerfTest.BizTalk.Biztalk -tp 100 -w 20 -d 60 -m Run > ../../../../../%1_2.txt
timeout /t 125
dotnet  PerfMark.dll -a BizTalk.dll -c PerfTest.BizTalk.Biztalk -tp 100 -w 20 -d 60 -m Run > ../../../../../%1_3.txt
cd ..\..\..\..\..\