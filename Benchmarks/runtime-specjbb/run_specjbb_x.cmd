timeout /t 300

"C:\Program Files (x86)\Windows Kits\10\Windows Performance Toolkit\xperf" -on PROC_THREAD+LOADER+INTERRUPT+DPC+CSWITCH+POWER+IDLE_STATES+TIMER+CLOCKINT+IPI+POWER+DISK_IO+DISK_IO_INIT+FILE_IO+FILE_IO_INIT -stackwalk CSwitch+ReadyThread+TimerSetPeriodic+TimerSetOneShot+CcFlushCache+CcFlushSection -clocktype perfcounter -buffersize 1024 -MinBuffers 1024
"C:\Program Files (x86)\Windows Kits\10\Windows Performance Toolkit\xperf" -start user -on Microsoft-Windows-Kernel-Power+Microsoft-Windows-Kernel-Processor-Power+Microsoft-Windows-PDC+Microsoft-Windows-Kernel-PEP+769E2C50-3A90-4894-A711-DBE6FF73A5D1+c88b592b-6090-480f-a839-ca2434de5844
"C:\Program Files (x86)\Windows Kits\10\Windows Performance Toolkit\xperf" -capturestate user Microsoft-Windows-Kernel-Power+Microsoft-Windows-Kernel-Processor-Power+Microsoft-Windows-PDC+Microsoft-Windows-Kernel-PEP+769E2C50-3A90-4894-A711-DBE6FF73A5D1+c88b592b-6090-480f-a839-ca2434de5844

cd C:\Users\wos\Desktop\Laxman\runtime-specjbb2005
dotnet.exe run -c Release -p .\specjbb.csproj > ../%1_1

"C:\Program Files (x86)\Windows Kits\10\Windows Performance Toolkit\xperf"  -flush
"C:\Program Files (x86)\Windows Kits\10\Windows Performance Toolkit\xperf"  -flush user
"C:\Program Files (x86)\Windows Kits\10\Windows Performance Toolkit\xperf"  -stop
"C:\Program Files (x86)\Windows Kits\10\Windows Performance Toolkit\xperf"  -stop user
"C:\Program Files (x86)\Windows Kits\10\Windows Performance Toolkit\xperf"  -merge \kernel.etl \user.etl ../workload_%1%.etl