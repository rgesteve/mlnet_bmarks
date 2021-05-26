cd C:\Users\wos\Desktop\PTMP-Beta-1-2-0928\CommandLine_Stats_Reader
Stats_Reader.exe 127.0.0.1 -start
start touchxprt2016://options/scenario=all/filename=%1
:WAITRESULT
IF EXIST C:\Users\wos\Pictures\TouchXPRT2016\Docs\%1 GOTO DONE
timeout /t 5 /nobreak > nul
goto WAITRESULT
:DONE
taskkill /F /IM TouchXPRT.exe
Stats_Reader.exe 127.0.0.1 -stop C:\Users\wos\Desktop\Laxman\data\%1
