#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#ifdef _MSC_VER
//
// cl -c -Zi -O2  -GL  -Oi- /EHsc /FAsc  /Fo.\memcpy_test_MSVC.obj  -Isrc  memcpy_test_MSVC.c
// link /debug /LTCG /out:memcpy_test_MSVC.exe memcpy_test_MSVC.obj
//
#include <intrin.h>
#pragma intrinsic(__movsb)
#pragma intrinsic(__rdtsc)
#else
#define noinline inline __attribute__((noinline))
#endif // _MSC_VER

typedef uint64_t u64;

#ifdef _MSC_VER

static __forceinline u64 rdtsc(void)
{
    return __rdtsc();
}

static __forceinline void repmov(void * dst, const void * src, size_t n)
{    
    __movsb(dst, src, n);
}

static __declspec (noinline) void memcpy32(char * dst, char * src, char * rnd)
{
    for (char * len = rnd; len < rnd + 65536; len += 256)
    {
        for (int i = 0; i < 256; i++)
        {
            memcpy(dst + 32 * i, src + 32 * i, 32);
        }
    }
}

static __declspec (noinline) void memcpyr(char * dst, char * src, char * rnd)
{
    for (char * len = rnd; len < rnd + 65536; len += 256)
    {
        for (int i = 0; i < 256; i++)
        {
            memcpy(dst + 32 * i, src + 32 * i, rnd[i]);
        }
    }
}

static __declspec (noinline) void repmov32(char * dst, char * src, char * rnd)
{
    for (char * len = rnd; len < rnd + 65536; len += 256)
    {
        for (int i = 0; i < 256; i++)
        {
            repmov(dst + 32 * i, src + 32 * i, 32);
        }
    }
}

static __declspec (noinline) void repmovr(char * dst, char * src, char * rnd)
{
    for (char * len = rnd; len < rnd + 65536; len += 256)
    {
        for (int i = 0; i < 256; i++)
        {
            repmov(dst + 32 * i, src + 32 * i, rnd[i]);
        }
    }
}

#else
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

#endif // _MSC_VER



int main(void)
{
    char src[8192];
    char dst[8192];
    char pad[65536];
    char rnd[65536];

#ifdef _MSC_VER
    // use same seed to get the same run to distribution
    srand(1);
    for (int i = 0; i < 65536; i++)
    {
        rnd[i] = rand() & 31;
    }

#else
    for (int i = 0; i < 65536; i++)
    {
        rnd[i] = random() & 31;
    }
#endif // __MSC_VER

    memcpy(src, rnd, sizeof(src));
    memset(dst, 0, sizeof(dst));
    memset(pad, 0, sizeof(pad));

    printf("      memcpy 32 bytes: ");

    for (int k = 0; k < 1; k++)
    {
        u64 time = rdtsc();
        memcpy32(dst, src, rnd);
        time = rdtsc() - time;
        printf("%8lld\n", time);
    }

    printf("memcpy random lengths: ");

    for (int k = 0; k < 1; k++)
    {
        u64 time = rdtsc();
        memcpyr(dst, src, rnd);
        time = rdtsc() - time;
        printf("%8lld\n", time);
    }

    printf("      repmov 32 bytes: ");

    for (int k = 0; k < 1; k++)
    {
        u64 time = rdtsc();
        repmov32(dst, src, rnd);
        time = rdtsc() - time;
        printf("%8lld\n", time);
    }

    printf("repmov random lengths: ");

    for (int k = 0; k < 1; k++)
    {
        u64 time = rdtsc();
        repmovr(dst, src, rnd);
        time = rdtsc() - time;
        printf("%8lld\n", time);
    }
}
