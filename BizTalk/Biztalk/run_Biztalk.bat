@rem @if {%1} == {} goto :usage

PerfMark.exe -a BizTalk.dll -c PerfTest.BizTalk.Biztalk -tc %NUMBER_OF_PROCESSORS% -w 20 -d 60 -m Run >> %1
PerfMark.exe -a BizTalk.dll -c PerfTest.BizTalk.Biztalk -tc %NUMBER_OF_PROCESSORS% -w 20 -d 60 -m Run >> %1
PerfMark.exe -a BizTalk.dll -c PerfTest.BizTalk.Biztalk -tc %NUMBER_OF_PROCESSORS% -w 20 -d 60 -m Run >> %1
PerfMark.exe -a BizTalk.dll -c PerfTest.BizTalk.Biztalk -tp 100 -w 20 -d 60 -m Run >> %1
PerfMark.exe -a BizTalk.dll -c PerfTest.BizTalk.Biztalk -tp 100 -w 20 -d 60 -m Run >> %1
PerfMark.exe -a BizTalk.dll -c PerfTest.BizTalk.Biztalk -tp 100 -w 20 -d 60 -m Run >> %1
goto :End

:usage
@echo Workload.exe [filename]
@echo filename = where the output of the benchmark will be stored

:End