//------------------------------------------------------------------
//
// global_helpers.h : 
//      1. defines globals used in shmoo
//      2. Defines  helpers  that operate on the globals       
//------------------------------------------------------------------
#include<malloc.h>

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
#define MAX_INTERVAL 5

unsigned int cpy_lengths[2050] = { 0 };
unsigned int smallcopies = 0;

enum MemOpType { kDoMemCpy, kDoMemSet };
// define some globals for program control
bool fPseudorandomCopy, fVerbose, fWarmup, fInterfere;
MemOpType fDoMemOp;
int iterations;
int repetitions = 1;
int swapsrcdst = 0;
int countinterval = MAX_INTERVAL;
int interferflag = 0;

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