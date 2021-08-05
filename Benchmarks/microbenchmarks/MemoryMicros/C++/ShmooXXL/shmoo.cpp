#include "shmoo.h"
#include "global_helpers.h"
#include <iostream>

// disable optimization to have comparable code sequence in
// in timing region
//#pragma optimize("", off) 
int _tmain(int argc, _TCHAR* argv[])
{
   	LARGE_INTEGER lFreq, lTime1, lTime2, diffTicks[MAX_RUNS]; 

	parse_cmd_line(argc, argv);

	ubn::time::Timer timer(true);
	
	initialize_run();
	
	//if (fVerbose)	
	if (fDoMemOp == kDoMemCpy)
			printf("Memcpy: Starting timing run for %d iterations with src_addr=%p dst_addr=%p\n", iterations,  g_psrc, g_pdst);
		else
			printf("Membet: Starting timing run for %d iterations with dst_addr=%p\n", iterations, g_pdst);
    
    
	std::cout << "Elapsed time: " << std::fixed << timer << "ms\n";
    timer.Reset();
	
   for(int j=0;j<repetitions;j++) {  
    // start the timing measurment
    QueryPerformanceFrequency(&lFreq);
    QueryPerformanceCounter(&lTime1);
	
	

	if (fDoMemOp == kDoMemCpy)
	{
		// do memory copy operations
		for (int j=0; j < iterations; j++)
		{
			for (int i=0; i < 2050; i++)    
			{
#ifdef USE_STDLIB_VERSION
				//memcpy(g_pdst, g_psrc, cpy_lengths[i]);
				JIT_MemCpy(g_pdst, g_psrc, cpy_lengths[i]);
				//JIT_MemCpy(g_pdst, g_psrc, cpy_lengths[i]);
#else
				 __movsb(g_pdst, g_psrc, cpy_lengths[i]);
#endif
			}
		}
	}
	else
	{
		// do memory set operations
		for (int j=0; j < iterations; j++)
		{
			for (int i = 0; i < 2050; i++)    
			{		
#ifdef USE_STDLIB_VERSION
				//memset(g_pdst, 0x0, cpy_lengths[i]);
				JIT_MemSet(g_pdst, 0x0, cpy_lengths[i]);
				JIT_MemSet(g_pdst, 0x0, cpy_lengths[i]);
#else
				__stosb(g_pdst, 0x0, cpy_lengths[i]);
#endif
			}
		}
	}
   // stop the timing 
    QueryPerformanceCounter(&lTime2);
	
	auto elapsed = timer.Elapsed();
    std::cout << "Elapsed time: " << std::fixed << elapsed.count() << "ms\n";
    
	// Print the statistics
	diffTicks[j].QuadPart = lTime2.QuadPart-lTime1.QuadPart;            //ticks passed
}
if(repetitions == 1) {
    double t3 =  (((double) diffTicks[0].QuadPart)/((double)lFreq.QuadPart));  //secs passed.

	// compute how many total bytes we copied
	__int64 bytes_copied = 0;
    for (int i = 0; i < 2050; i++)         
          bytes_copied += cpy_lengths[i];   

	printf("ubn Result for %s: ticks_elapsed : %I64i    Duration(seconds) : %.2f  Bytes %s : %I64i Proc Freq: %I64i\n", (fDoMemOp == kDoMemCpy) ? "Memory copy" : "Memory set", diffTicks[0].QuadPart, t3, (fDoMemOp == kDoMemCpy) ? "copied" : "cleared", bytes_copied, lFreq.QuadPart);

	}
else {
		printf("ubn Result for %s: ticks_elapsed:\n \n",(fDoMemOp == kDoMemCpy) ? "Memory copy" : "Memory set");
		for (int j=0; j < repetitions; j++)
		   printf("%I64i\n",diffTicks[j].QuadPart);
		{
		printf("\n");
	  }
	}
	return 0;
}

