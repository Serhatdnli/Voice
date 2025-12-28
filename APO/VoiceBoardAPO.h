#pragma once

#include <windows.h>
#include <initguid.h>
#include <mmdeviceapi.h>
#include <audioenginebaseapo.h>
#include <audioapotypes.h>
#include <atomic>

// VoiceBoardAPO CLSID - {8A5BC3E7-9D4F-4B2A-8C1E-6F3D5A7B9C0E}
DEFINE_GUID(CLSID_VoiceBoardAPO,
    0x8a5bc3e7, 0x9d4f, 0x4b2a, 0x8c, 0x1e, 0x6f, 0x3d, 0x5a, 0x7b, 0x9c, 0x0e);

// Named pipe for communication with C# app
#define VOICEBOARD_PIPE_NAME L"\\\\.\\pipe\\VoiceBoardAudio"

// Shared memory for audio data
#define VOICEBOARD_SHARED_MEM_NAME L"VoiceBoardSharedMem"
#define VOICEBOARD_SHARED_MEM_SIZE (48000 * 2 * sizeof(float) * 2) // 2 seconds stereo float

// Shared memory structure
struct VoiceBoardSharedData
{
    std::atomic<bool> isPlaying;
    std::atomic<UINT32> writePos;
    std::atomic<UINT32> readPos;
    UINT32 sampleRate;
    UINT32 channels;
    UINT32 bufferSize;
    float audioBuffer[48000 * 2 * 2]; // 2 seconds, stereo
};

class VoiceBoardAPO : public IAudioProcessingObject,
                       public IAudioProcessingObjectConfiguration,
                       public IAudioSystemEffects
{
public:
    VoiceBoardAPO();
    virtual ~VoiceBoardAPO();

    // IUnknown
    STDMETHOD(QueryInterface)(REFIID riid, void** ppv);
    STDMETHOD_(ULONG, AddRef)();
    STDMETHOD_(ULONG, Release)();

    // IAudioProcessingObject
    STDMETHOD(Reset)();
    STDMETHOD(GetLatency)(HNSTIME* pTime);
    STDMETHOD(GetRegistrationProperties)(APO_REG_PROPERTIES** ppRegProps);
    STDMETHOD(Initialize)(UINT32 cbDataSize, BYTE* pbyData);
    STDMETHOD(IsInputFormatSupported)(
        IAudioMediaType* pOutputFormat,
        IAudioMediaType* pRequestedInputFormat,
        IAudioMediaType** ppSupportedInputFormat);
    STDMETHOD(IsOutputFormatSupported)(
        IAudioMediaType* pInputFormat,
        IAudioMediaType* pRequestedOutputFormat,
        IAudioMediaType** ppSupportedOutputFormat);
    STDMETHOD(GetInputChannelCount)(UINT32* pu32ChannelCount);

    // IAudioProcessingObjectConfiguration
    STDMETHOD(LockForProcess)(
        UINT32 u32NumInputConnections,
        APO_CONNECTION_DESCRIPTOR** ppInputConnections,
        UINT32 u32NumOutputConnections,
        APO_CONNECTION_DESCRIPTOR** ppOutputConnections);
    STDMETHOD(UnlockForProcess)();

    // IAudioSystemEffects - marker interface, no methods

    // Audio processing
    void Process(UINT32 u32NumInputFrames, float* pInput, float* pOutput);

private:
    LONG m_refCount;
    bool m_isLocked;
    UINT32 m_samplesPerFrame;
    UINT32 m_bytesPerSample;

    // Shared memory
    HANDLE m_hSharedMem;
    VoiceBoardSharedData* m_pSharedData;

    void OpenSharedMemory();
    void CloseSharedMemory();
};

// Class factory
class VoiceBoardAPOFactory : public IClassFactory
{
public:
    // IUnknown
    STDMETHOD(QueryInterface)(REFIID riid, void** ppv);
    STDMETHOD_(ULONG, AddRef)();
    STDMETHOD_(ULONG, Release)();

    // IClassFactory
    STDMETHOD(CreateInstance)(IUnknown* pUnkOuter, REFIID riid, void** ppv);
    STDMETHOD(LockServer)(BOOL fLock);
};

// Global factory instance
extern VoiceBoardAPOFactory g_Factory;
extern HMODULE g_hModule;
extern LONG g_serverLocks;
