// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

class CrashInfo;

class DataTarget : public ICLRDataTarget, ICorDebugDataTarget4
{
private:
    LONG m_ref;                         // reference count
    pid_t m_pid;                        // process id
    int m_fd;                           // /proc/<pid>/mem handle
    CrashInfo* m_crashInfo;

public:
    DataTarget(pid_t pid);
    virtual ~DataTarget();
    bool Initialize(CrashInfo* crashInfo);

    //
    // IUnknown
    //
    STDMETHOD(QueryInterface)(___in REFIID InterfaceId, ___out PVOID* Interface);
    STDMETHOD_(ULONG, AddRef)();
    STDMETHOD_(ULONG, Release)();

    //
    // ICLRDataTarget
    //
    virtual HRESULT STDMETHODCALLTYPE GetMachineType( 
        /* [out] */ ULONG32 *machine);

    virtual HRESULT STDMETHODCALLTYPE GetPointerSize( 
        /* [out] */ ULONG32 *size);

    virtual HRESULT STDMETHODCALLTYPE GetImageBase( 
        /* [string][in] */ LPCWSTR moduleName,
        /* [out] */ CLRDATA_ADDRESS *baseAddress);

    virtual HRESULT STDMETHODCALLTYPE ReadVirtual( 
        /* [in] */ CLRDATA_ADDRESS address,
        /* [length_is][size_is][out] */ PBYTE buffer,
        /* [in] */ ULONG32 size,
        /* [optional][out] */ ULONG32 *done);

    virtual HRESULT STDMETHODCALLTYPE WriteVirtual( 
        /* [in] */ CLRDATA_ADDRESS address,
        /* [size_is][in] */ PBYTE buffer,
        /* [in] */ ULONG32 size,
        /* [optional][out] */ ULONG32 *done);

    virtual HRESULT STDMETHODCALLTYPE GetTLSValue(
        /* [in] */ ULONG32 threadID,
        /* [in] */ ULONG32 index,
        /* [out] */ CLRDATA_ADDRESS* value);

    virtual HRESULT STDMETHODCALLTYPE SetTLSValue(
        /* [in] */ ULONG32 threadID,
        /* [in] */ ULONG32 index,
        /* [in] */ CLRDATA_ADDRESS value);

    virtual HRESULT STDMETHODCALLTYPE GetCurrentThreadID(
        /* [out] */ ULONG32* threadID);

    virtual HRESULT STDMETHODCALLTYPE GetThreadContext(
        /* [in] */ ULONG32 threadID,
        /* [in] */ ULONG32 contextFlags,
        /* [in] */ ULONG32 contextSize,
        /* [out, size_is(contextSize)] */ PBYTE context);

    virtual HRESULT STDMETHODCALLTYPE SetThreadContext(
        /* [in] */ ULONG32 threadID,
        /* [in] */ ULONG32 contextSize,
        /* [in, size_is(contextSize)] */ PBYTE context);

    virtual HRESULT STDMETHODCALLTYPE Request( 
        /* [in] */ ULONG32 reqCode,
        /* [in] */ ULONG32 inBufferSize,
        /* [size_is][in] */ BYTE *inBuffer,
        /* [in] */ ULONG32 outBufferSize,
        /* [size_is][out] */ BYTE *outBuffer);

    //
    // ICorDebugDataTarget4
    //
    virtual HRESULT STDMETHODCALLTYPE VirtualUnwind(
        /* [in] */ DWORD threadId,
        /* [in] */ ULONG32 contextSize,
        /* [in, out, size_is(contextSize)] */ PBYTE context);
};