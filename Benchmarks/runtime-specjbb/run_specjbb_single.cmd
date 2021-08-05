cd .\runtime-specjbb2005
dotnet.exe publish -c Release .\specjbb.csproj
timeout /t 130
dotnet.exe .\bin\Release\netcoreapp2.0\publish\specjbb.dll > ../%1_1.txt
cd ..