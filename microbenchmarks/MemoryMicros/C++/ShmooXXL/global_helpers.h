//------------------------------------------------------------------
//
// global_helpers.h : 
//      1. defines globals used in shmoo
//      2. Defines  helpers  that operate on the globals       
//------------------------------------------------------------------
#include<malloc.h>
#include<map>
#include<utility>
//ALIGN_64BYTE BYTE src[BLOB_SIZE];
//ALIGN_64BYTE BYTE dst[BLOB_SIZE];
BYTE *src;
BYTE *dst;

#define MEMCPY_NT_ITERS 8239 
#define MEMCPY_NT_BLOCK_SIZE 128 

PBYTE g_psrc = NULL;
PBYTE g_pdst = NULL;
UINT64 src_offset, dst_offset;
extern "C" unsigned int __favor;

//extern "C" unsigned _int64 __memcpy_nt_iters;

unsigned _int64 __memcpy_nt_iters;

#define MAX_RUNS 11
#define MAX_INTERVAL 10

unsigned int cpy_lengths[2050] = {0};
unsigned int smallcopies =0;

enum MemOpType { kDoMemCpy, kDoMemSet };
// define some globals for program control
bool fPseudorandomCopy, fVerbose, fWarmup, fInterfere;
MemOpType fDoMemOp;
int iterations;
int repetitions=1;
int swapsrcdst = 0;
int countinterval=MAX_INTERVAL;
int interferflag=0;

void parse_cmd_line(int argc, _TCHAR* argv[])
{
	fPseudorandomCopy = fVerbose = fWarmup = false;
	src_offset = dst_offset = 0;
	iterations = DEFAULT_ITERATIONS;
	fDoMemOp = kDoMemCpy;
#ifdef USE_ITER2
	__memcpy_nt_iters2 = (MEMCPY_NT_ITERS * MEMCPY_NT_BLOCK_SIZE);
#endif

	_TCHAR parseline[1024] = {0};
	
	if (argc > 1)
	{		
		for (int i = 1; i < argc; i++)
			strcat(parseline, argv[i]);
		
		char *pdest = strstr(parseline, "PseudorandomCopy");
		if (pdest)
		{
			char *itr = strstr(pdest, "=");
			if (itr) {
				itr +=1;
				if (atoi(itr) != 0)
					fPseudorandomCopy = true;
			}
			else
				fPseudorandomCopy = true;	
		}


		pdest = strstr(parseline, "Verbose");
		if (pdest)
		{
			char *itr = strstr(pdest, "=");
			if (itr) {
				itr +=1;
				if (atoi(itr) != 0)
					fVerbose = true;
			}
			else
				fVerbose = true;	
		}

		// Interfere=1 src and dst addresses interfere
		//
		pdest = strstr(parseline, "interfere");
		if (pdest)
		{
			char *itr = strstr(pdest, "=");
			if (itr) {
				itr +=1;
				interferflag = atoi(itr);
				switch (interferflag)
				{
				    case 0:
				        fInterfere = false;
				        break;

				    case 1:
				        fInterfere = true;
				        break;

				    case 2:
				        fInterfere = false;	
				        __favor = __favor & 0xFFFB; //disable xmmloop
				        break;

				    case 3:
				        fInterfere = true;
				        __favor = __favor & 0xFFFB;	//disable xmmloop		        
				        break;

				    case 4:
				        fInterfere = false;
         				__memcpy_nt_iters = MEMCPY_NT_ITERS;
				        __favor = __favor & 0xFFFD; //disable enhanced fast strings forcing non-temporal
				        break;


				    default:
				        fInterfere = false;
				        break;
				}
			}
				    
		}

		pdest = strstr(parseline, "DoMemOp");
		if (pdest)
		{
			char *itr = strstr(pdest, "=");
			if (itr) {
				itr +=1;
				if (strncmp(itr, "clr", 3) == 0 ||
						strncmp(itr, "set", 3) == 0)
					fDoMemOp = kDoMemSet;	
			}			
		}		

		pdest = strstr(parseline, "Warmup");
		if (pdest)
		{
			char *itr = strstr(pdest, "=");
			if (itr) {
				itr +=1;
				if (atoi(itr) != 0)
					fWarmup = true;
			}
			else
				fWarmup = true;	
		}

		pdest = strstr(parseline, "iterations");
		if (pdest)
		{
			char *itr = strstr(pdest, "=");
			itr +=1;
			iterations = atoi(itr);
		}
        
        // with PseudorandomCopy=1
		// smallcopies>4096 add smallcopies to random lengths between 1-2050 
		// smallcopies<4096 change (random lengths between 1-2050 ) % smallcopies
        pdest = strstr(parseline, "smallcopies");
		if (pdest)
		{
			char *itr = strstr(pdest, "=");
			itr +=1;
			smallcopies = atoi(itr);
		}

		pdest = strstr(parseline, "src_offset");
		if (pdest)
		{
			char *itr = strstr(pdest, "=");
			itr +=1;
			src_offset = (UINT64) atoi(itr);
		}

		pdest = strstr(parseline, "dst_offset");
		if (pdest)
		{
			char *itr = strstr(pdest, "=");
			itr +=1;
			dst_offset = (UINT64) atoi(itr);
		}	
		
		pdest = strstr(parseline, "repetitions");
		if (pdest)
		{
			char *itr = strstr(pdest, "=");
			itr +=1;
			repetitions = atoi(itr);
			if(repetitions > MAX_RUNS) {
				printf("*** Repetitions %i is greater than the MAX %i Using Max ****\n",repetitions,MAX_RUNS);
				repetitions=MAX_RUNS;
		  }	
        }

        // with PseudorandomCopy=1
		// countinterval=0 small copies    between 1-16 
		// countinterval=1 medium copies   between 17-128 
		// countinterval=2 larger copies   between 128-2050 
		// countinterval=3 random lengths  between 1-2050 last length is 4096
		// countinterval=4 extra large     between 4096-8396800  
		// countinterval=5 extra XXL       between 1048576-33554432  
		pdest = strstr(parseline, "countinterval");
		if (pdest)
		{
			char *itr = strstr(pdest, "=");
			itr +=1;
			countinterval = atoi(itr);
			if(countinterval > MAX_INTERVAL) { 
				printf("*** Repetitions %i is greater than the MAX %i Using Max ****\n",countinterval,MAX_INTERVAL);
				countinterval=MAX_INTERVAL;
		  }
          if(countinterval<10) {
             src = (unsigned char*) _aligned_malloc(BLOB_SIZE, 64);
             dst = (unsigned char*) _aligned_malloc(BLOB_SIZE, 64);
          }
          else {
             src = (unsigned char*) _aligned_malloc((MEMCPY_NT_ITERS * MEMCPY_NT_BLOCK_SIZE) + (34 * 4096), 64);
             dst = (unsigned char*) _aligned_malloc((MEMCPY_NT_ITERS * MEMCPY_NT_BLOCK_SIZE) + (34 * 4096), 64);
            
          }


        }
	}
	else
	{
		printf("Usage:	\
			   \n%s -PseudorandomCopy=[0|1]  -Warmup=[0,1] -DoMemOp=[cpy|set] -iterations=[int] -src_offset=[int] -dst_offset=[int] -Verbose=[0,1]\n",argv[0]);
	}
	if (fVerbose)
		printf("arguments:				\
			   \n\titerations: %d		\
		\n\tpPseudorandomCopy: %d		\
		\n\tpVerbose:%d					\
		\n\tpWarmup:%d					\
		\n\tpDoMemOp:%s				\
		\n\tpSrcOffset: %I64i			\
		\n\tpDstOffset: %I64i\n", iterations, fPseudorandomCopy, fVerbose,  fWarmup, fDoMemOp == kDoMemCpy ? "memcpy" : "memset" , src_offset, dst_offset);
}

void set_src_and_dst_addr()
{
	unsigned __int64 a = (unsigned __int64)src >> 12 ;  	
	unsigned __int64 b = a * PAGESIZE ;					// current 4K baseaddr
    PBYTE temp;
    
	PBYTE src_p = (PBYTE) (b  + PAGESIZE );				// start of next 4K
	g_psrc = (PBYTE) ((unsigned __int64)src_p + src_offset);

	// do the same for the destination
	a = (unsigned __int64)dst >> 12 ;  	
	b = a * PAGESIZE ;									// current 4K baseaddr
	
	PBYTE dst_p = (PBYTE) (b  + PAGESIZE );	            // start of next 4K
	g_pdst = (PBYTE) ((unsigned __int64)dst_p + dst_offset);
	
	if(swapsrcdst) {
	    temp = g_psrc;
	    g_psrc = g_pdst;
	    g_pdst = temp;
	}
    
    if(fInterfere)
        g_pdst = g_psrc+1;
	if (fVerbose)
		printf("src_addr: %p  dst_addr: %p\n", g_psrc, g_pdst);
}

void get_copy_length_distribution(bool fDoPseudorandomCopy)
{
    int k;
    
    
	if (fDoPseudorandomCopy)
	{
		// do copy in psuedo rando order
		for(int j=0; j<2050;j++)
			cpy_lengths[j] = TO_BE_FILLED;

		// use same seed to get the same run to distribution
		srand(1);
		for(int count=0; count<2050;count++)
		{
              
			int index = (rand() % 2051);
      
			while (cpy_lengths[index] != TO_BE_FILLED )
			{
				index = (rand() % 2051);
			}
		    cpy_lengths[index] = count; 
		    if(smallcopies) {
		        if(smallcopies>=4096) {
		            cpy_lengths[index] += smallcopies; 
		        }
		        else {
		            cpy_lengths[index] = cpy_lengths[index] % smallcopies; 		
		            while(cpy_lengths[index] == 0) cpy_lengths[index] = (rand() % smallcopies);
		        }
			}
			else {
    			//if(countinterval==5) {
    			//    cpy_lengths[index] = (MEMCPY_NT_ITERS * MEMCPY_NT_BLOCK_SIZE)  + (((count % 33)+1) * 4096);     
    			//}
    			// if(countinterval==4) {
				// 	k = count % 501;
    			//     if(k < 129) k = 129 + (rand() % (501-129));
    			//     cpy_lengths[index] = k;  
				// }
    			// if(countinterval==3) {
				// 	k = count % 129;
    			//     if(k < 65) k = 65 + (rand() % (129-65));
    			//     cpy_lengths[index] = k;   
				// }
				// if(countinterval==2) {
    			//     k = count % 65;
    			//     if(k < 33) k = 33 + (rand() % (65-33));
    			//     cpy_lengths[index] = k;              
    			// }
				// if(countinterval==1) {
    			//     k = count % 33;
    			//     if(k < 17) k = 17 + (rand() % (33-17));
    			//     cpy_lengths[index] = k;              
    			// }
    			// if(countinterval==0) cpy_lengths[index] = count % 17;   
#if 0
				std::map<int, std::pair<int, int>> range_map;
				range_map[0] = std::make_pair<int,int>(0, 8);
				range_map[1] = std::make_pair(9, 17);

				//assert(range_map.find(counterinterval) != range_map.end());					
				std::pair<int, int> range = range_map[countinterval];
				
				int range_min = range.first;
				int range_max = range.second;
				k = count % range_max;
				if(k < range_min) k = range_min + (rand() % (range_max - range_min));
				cpy_lengths[index] = k;

#endif
				if(countinterval==5) {
    			    cpy_lengths[index] = (MEMCPY_NT_ITERS * MEMCPY_NT_BLOCK_SIZE)  + (((count % 33)+1) * 4096);     
    			}
				if(countinterval==9) {
					k = count % 4097;
    			    if(k < 2049) k = 2049 + (rand() % (4097-2049));
    			    cpy_lengths[index] = k;  
				}
				if(countinterval==8) {
					k = count % 2049;
    			    if(k < 1025) k = 1025 + (rand() % (2049-1025));
    			    cpy_lengths[index] = k;  
				}
				if(countinterval==7) {
					k = count % 1025;
    			    if(k < 513) k = 513 + (rand() % (1025-513));
    			    cpy_lengths[index] = k;  
				}
				if(countinterval==6) {
					k = count % 513;
    			    if(k < 257) k = 257 + (rand() % (513-257));
    			    cpy_lengths[index] = k;  
				}
				if(countinterval==5) {
					k = count % 257;
    			    if(k < 129) k = 129 + (rand() % (257-129));
    			    cpy_lengths[index] = k;  
				}
				if(countinterval==4) {
					k = count % 129;
    			    if(k < 65) k = 65 + (rand() % (129-65));
    			    cpy_lengths[index] = k;  
				}
    			if(countinterval==3) {
					k = count % 65;
    			    if(k < 33) k = 33 + (rand() % (65-33));
    			    cpy_lengths[index] = k;   
				}
				if(countinterval==2) {
    			    k = count % 33;
    			    if(k < 17) k = 17 + (rand() % (33-17));
    			    cpy_lengths[index] = k;              
    			}
				if(countinterval==1) {
    			    k = count % 17;
    			    if(k < 9) k = 9 + (rand() % (17-9));
    			    cpy_lengths[index] = k;              
    			}
    			if(countinterval==0) cpy_lengths[index] = count % 9;   //Will give 0 to 8  
    			
    			
    		}
		}
	}
	else
	{
		// do copies in order
		for (int l = 0; l<2050; l++) {
			cpy_lengths[l] = 0; 
		    if(smallcopies) cpy_lengths[l] = cpy_lengths[l] % smallcopies;
		}
	}
	// finish with a 4K copy for both inorder and psuedo random
	// cpy_lengths[2049] = 4096;
	//if(countinterval==3) cpy_lengths[2049] = 4096; 		
	
	
	if (fVerbose)
	{
		printf("Printing the copy operation lengths distribution we will use\n");
		printf("%s----\n", fDoPseudorandomCopy? "Pseudorando distribution" : "in order distribution");
		for(int i=0; i<2050;i++)
			printf("copy  number= :  %d ,	len= : %d\n", i,cpy_lengths [i]); 
		printf("done printing copy operation lengths distribution\n\n");
	__int64 bytes_copied = 0;
    for (int i = 0; i < 2050; i++)         
          bytes_copied += cpy_lengths[i];   

	printf("Bytes copied : %I64i\n",bytes_copied);
	}
}

void do_memcpy(int len)
{
#ifdef USE_STDLIB_VERSION
  //memcpy(g_psrc, g_pdst, len);
  JIT_MemCpy(g_psrc, g_pdst, len);
#else
  __movsb(g_psrc, g_pdst, len);  	
#endif
}

void do_memset(int len)
{
#ifdef USE_STDLIB_VERSION
  //(g_pdst, 0x0, len);
  JIT_MemSet(g_pdst, 0x0, len);
#else
    __stosb(g_pdst, 0x0, len);
#endif
}

void do_warmup(int len)
{
	if (fVerbose)
	{
		if (fDoMemOp == kDoMemCpy)
			printf("memcpy doing warmup of length=%d src_addr=%p dst_addr=%p\n", len,  g_psrc, g_pdst);
		else
			printf("memset doing warmup of length=%d dst_addr=%p\n", len, g_pdst);
	}
	
	if (fDoMemOp == kDoMemCpy)		
		do_memcpy(len);
	else
		do_memset(len);
}

void initialize_run()
{
	set_src_and_dst_addr();

	get_copy_length_distribution(fPseudorandomCopy);

	if (fWarmup)
		do_warmup(BLOB_4K);
}

#if __cplusplus < 201103L && (!defined(_MSC_VER) || _MSC_VER < 1700)
#error ubn::Timer class requires C++11 support
#else
#include <chrono>
#include <ostream>
namespace ubn {
    namespace time {
        class Timer {
            typedef std::chrono::high_resolution_clock high_resolution_clock;
            typedef std::chrono::milliseconds milliseconds;
        public:
            explicit Timer(bool run = false)
            {
                if (run)
                    Reset();
            }
            void Reset()
            {
                _start = high_resolution_clock::now();
            }
            milliseconds Elapsed() const
            {
                return std::chrono::duration_cast<milliseconds>(high_resolution_clock::now() - _start);
            }
            template <typename T, typename Traits>
            friend std::basic_ostream<T, Traits>& operator<<(std::basic_ostream<T, Traits>& out, const Timer& timer)
            {
                return out << timer.Elapsed().count();
            }
        private:
            high_resolution_clock::time_point _start;
        };
    }
}
#endif