#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

// The default way folks are building and running the code == the compilation machine == the test machine, ergo the full compiler command line is:
//
//                gcc -Wall -O2 -march=native
//
// To cross compile, you would explicitly use -march to target the machine for execution
// 
// gcc -Wall -O2 -march=icelake-client|icelake-server|tigerlake


#define noinline inline __attribute__((noinline))
typedef uint64_t u64;

static inline u64 rdtsc(void)
{
    unsigned int low, high;

    asm volatile("rdtsc" : "=a"(low), "=d"(high));
    return low | ((u64) high) << 32;
}

static inline void repmov(void * dst, const void * src, size_t n)
{
    asm volatile("rep movsb" : "+D"(dst), "+S"(src), "+c"(n) ::);
}

static noinline void memcpy32(char * dst, char * src, char * rnd)
{
    for (char * len = rnd; len < rnd + 65536; len += 256)
    {
        for (int i = 0; i < 256; i++)
        {
            memcpy(dst + 32 * i, src + 32 * i, 32);
        }
    }
}

static noinline void memcpyr(char * dst, char * src, char * rnd)
{
    for (char * len = rnd; len < rnd + 65536; len += 256)
    {
        for (int i = 0; i < 256; i++)
        {
            memcpy(dst + 32 * i, src + 32 * i, rnd[i]);
        }
    }
}

static noinline void repmov32(char * dst, char * src, char * rnd)
{
    for (char * len = rnd; len < rnd + 65536; len += 256)
    {
        for (int i = 0; i < 256; i++)
        {
            repmov(dst + 32 * i, src + 32 * i, 32);
        }
    }
}

static noinline void repmovr(char * dst, char * src, char * rnd)
{
    for (char * len = rnd; len < rnd + 65536; len += 256)
    {
        for (int i = 0; i < 256; i++)
        {
            repmov(dst + 32 * i, src + 32 * i, rnd[i]);
        }
    }
}

int main(void)
{
    char src[8192];
    char dst[8192];
    char pad[65536];
    char rnd[65536];

    for (int i = 0; i < 65536; i++)
    {
        rnd[i] = random() & 31;
    }

    memcpy(src, rnd, sizeof(src));
    memset(dst, 0, sizeof(dst));
    memset(pad, 0, sizeof(pad));

    printf("      memcpy 32 bytes: ");

    for (int k = 0; k < 1; k++)
    {
        u64 time = rdtsc();
        memcpy32(dst, src, rnd);
        time = rdtsc() - time;
        printf("%8ld\n", time);
    }

    printf("memcpy random lengths: ");

    for (int k = 0; k < 1; k++)
    {
        u64 time = rdtsc();
        memcpyr(dst, src, rnd);
        time = rdtsc() - time;
        printf("%8ld\n", time);
    }

    printf("      repmov 32 bytes: ");

    for (int k = 0; k < 1; k++)
    {
        u64 time = rdtsc();
        repmov32(dst, src, rnd);
        time = rdtsc() - time;
        printf("%8ld\n", time);
    }

    printf("repmov random lengths: ");

    for (int k = 0; k < 1; k++)
    {
        u64 time = rdtsc();
        repmovr(dst, src, rnd);
        time = rdtsc() - time;
        printf("%8ld\n", time);
    }
}
