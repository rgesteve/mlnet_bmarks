//------------------------------------------------------------------
//
// ProtoLEA.h : 
//      1. Defines some types used in ProtoLEA.cpp        
//------------------------------------------------------------------
#include <stdio.h>
#include <tchar.h>
#include <stdlib.h>
#include <Windows.h>

#define BYTE   unsigned char 
#define PBYTE  unsigned char*
#define UINT64 unsigned __int64
#define PUINT64 unsigned __int64*
#define SIZE_T size_t

extern "C" void __movsb(PBYTE, BYTE const *, SIZE_T);
#pragma intrinsic(__movsb)

extern "C" void __stosb(PBYTE, BYTE, SIZE_T);
#pragma intrinsic(__stosb)

extern "C" unsigned __int64 __rdtsc(void);
#pragma intrinsic(__rdtsc)

#define ALIGN_16BYTE	__declspec(align(16))
#define ALIGN_32BYTE	__declspec(align(32))
#define ALIGN_64BYTE	__declspec(align(64))

#define KB 1024
#define MB 1024*KB
#define GB 1024*MB

#define BLOB_4K   4096
#define BLOB_SIZE               1*MB
//#define BLOB_SIZE				9*MB
#define NUM_REPEATS				1
#define DEFAULT_ITERATIONS		10000
#define  TO_BE_FILLED			7111

#define PAGEMASK	0xFFFFF000
#define PAGESIZE	4*KB
#define DEFAULT_ITERATIONS		10000
