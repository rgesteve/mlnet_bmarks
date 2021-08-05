set complus_buildflavor=svr
PerfMark.exe -a BizTalk.dll -c PerfTest.BizTalk.Biztalk -tc %NUMBER_OF_PROCESSORS% -w 20 -d 4800 -m Run 
