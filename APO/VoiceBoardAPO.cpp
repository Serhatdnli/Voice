#include "VoiceBoardAPO.h"
#include <new>
#include <cstring>

// Global variables
VoiceBoardAPOFactory g_Factory;
HMODULE g_hModule = NULL;
LONG g_serverLocks = 0;

// Registration properties
static APO_REG_PROPERTIES g_regProps = {
    CLSID_VoiceBoardAPO,
    APO_FLAG_DEFAULT,
    L"VoiceBoard Audio Injector",
    L"VoiceBoard",
    1, 0, // Version
    1, 1, // Min/Max input connections
    1, 1, // Min/Max output connections
    0,    // Max instances
    0, NULL // Interface count
};

//-----------------------------------------------------------------------------
// VoiceBoardAPO Implementation
//-----------------------------------------------------------------------------

VoiceBoardAPO::VoiceBoardAPO()
    : m_refCount(1)
    , m_isLocked(false)
    , m_samplesPerFrame(0)
    , m_bytesPerSample(sizeof(float))
    , m_hSharedMem(NULL)
    , m_pSharedData(nullptr)
{
    OpenSharedMemory();
}

VoiceBoardAPO::~VoiceBoardAPO()
{
    CloseSharedMemory();
}

void VoiceBoardAPO::OpenSharedMemory()
{
    m_hSharedMem = OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, VOICEBOARD_SHARED_MEM_NAME);
    if (m_hSharedMem)
    {
        m_pSharedData = (VoiceBoardSharedData*)MapViewOfFile(
            m_hSharedMem, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(VoiceBoardSharedData));
    }
}

void VoiceBoardAPO::CloseSharedMemory()
{
    if (m_pSharedData)
    {
        UnmapViewOfFile(m_pSharedData);
        m_pSharedData = nullptr;
    }
    if (m_hSharedMem)
    {
        CloseHandle(m_hSharedMem);
        m_hSharedMem = NULL;
    }
}

// IUnknown
STDMETHODIMP VoiceBoardAPO::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;

    if (riid == IID_IUnknown)
        *ppv = static_cast<IAudioProcessingObject*>(this);
    else if (riid == __uuidof(IAudioProcessingObject))
        *ppv = static_cast<IAudioProcessingObject*>(this);
    else if (riid == __uuidof(IAudioProcessingObjectConfiguration))
        *ppv = static_cast<IAudioProcessingObjectConfiguration*>(this);
    else if (riid == __uuidof(IAudioSystemEffects))
        *ppv = static_cast<IAudioSystemEffects*>(this);
    else
    {
        *ppv = NULL;
        return E_NOINTERFACE;
    }

    AddRef();
    return S_OK;
}

STDMETHODIMP_(ULONG) VoiceBoardAPO::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

STDMETHODIMP_(ULONG) VoiceBoardAPO::Release()
{
    LONG ref = InterlockedDecrement(&m_refCount);
    if (ref == 0) delete this;
    return ref;
}

// IAudioProcessingObject
STDMETHODIMP VoiceBoardAPO::Reset()
{
    return S_OK;
}

STDMETHODIMP VoiceBoardAPO::GetLatency(HNSTIME* pTime)
{
    if (!pTime) return E_POINTER;
    *pTime = 0;
    return S_OK;
}

STDMETHODIMP VoiceBoardAPO::GetRegistrationProperties(APO_REG_PROPERTIES** ppRegProps)
{
    if (!ppRegProps) return E_POINTER;

    *ppRegProps = (APO_REG_PROPERTIES*)CoTaskMemAlloc(sizeof(APO_REG_PROPERTIES));
    if (!*ppRegProps) return E_OUTOFMEMORY;

    memcpy(*ppRegProps, &g_regProps, sizeof(APO_REG_PROPERTIES));
    return S_OK;
}

STDMETHODIMP VoiceBoardAPO::Initialize(UINT32 cbDataSize, BYTE* pbyData)
{
    return S_OK;
}

STDMETHODIMP VoiceBoardAPO::IsInputFormatSupported(
    IAudioMediaType* pOutputFormat,
    IAudioMediaType* pRequestedInputFormat,
    IAudioMediaType** ppSupportedInputFormat)
{
    if (!ppSupportedInputFormat) return E_POINTER;

    // Accept any format, pass through
    if (pRequestedInputFormat)
    {
        pRequestedInputFormat->AddRef();
        *ppSupportedInputFormat = pRequestedInputFormat;
        return S_OK;
    }

    *ppSupportedInputFormat = NULL;
    return E_INVALIDARG;
}

STDMETHODIMP VoiceBoardAPO::IsOutputFormatSupported(
    IAudioMediaType* pInputFormat,
    IAudioMediaType* pRequestedOutputFormat,
    IAudioMediaType** ppSupportedOutputFormat)
{
    if (!ppSupportedOutputFormat) return E_POINTER;

    if (pRequestedOutputFormat)
    {
        pRequestedOutputFormat->AddRef();
        *ppSupportedOutputFormat = pRequestedOutputFormat;
        return S_OK;
    }

    *ppSupportedOutputFormat = NULL;
    return E_INVALIDARG;
}

STDMETHODIMP VoiceBoardAPO::GetInputChannelCount(UINT32* pu32ChannelCount)
{
    if (!pu32ChannelCount) return E_POINTER;
    *pu32ChannelCount = 2; // Stereo
    return S_OK;
}

// IAudioProcessingObjectConfiguration
STDMETHODIMP VoiceBoardAPO::LockForProcess(
    UINT32 u32NumInputConnections,
    APO_CONNECTION_DESCRIPTOR** ppInputConnections,
    UINT32 u32NumOutputConnections,
    APO_CONNECTION_DESCRIPTOR** ppOutputConnections)
{
    if (u32NumInputConnections != 1 || u32NumOutputConnections != 1)
        return E_INVALIDARG;

    m_isLocked = true;

    // Get format info from connection
    if (ppInputConnections && ppInputConnections[0])
    {
        UNCOMPRESSEDAUDIOFORMAT format;
        if (SUCCEEDED(ppInputConnections[0]->pFormat->GetUncompressedAudioFormat(&format)))
        {
            m_samplesPerFrame = format.dwSamplesPerFrame;
        }
    }

    return S_OK;
}

STDMETHODIMP VoiceBoardAPO::UnlockForProcess()
{
    m_isLocked = false;
    return S_OK;
}

// Audio processing - This is called by Windows Audio Engine
void VoiceBoardAPO::Process(UINT32 u32NumInputFrames, float* pInput, float* pOutput)
{
    if (!m_pSharedData || !pInput || !pOutput)
    {
        // Pass through if no shared memory
        if (pInput && pOutput)
            memcpy(pOutput, pInput, u32NumInputFrames * m_samplesPerFrame * sizeof(float));
        return;
    }

    // Check if VoiceBoard app is playing audio
    if (m_pSharedData->isPlaying.load())
    {
        // Read injected audio from shared memory
        UINT32 readPos = m_pSharedData->readPos.load();
        UINT32 writePos = m_pSharedData->writePos.load();
        UINT32 channels = m_pSharedData->channels;
        UINT32 bufferSize = m_pSharedData->bufferSize;

        for (UINT32 i = 0; i < u32NumInputFrames * m_samplesPerFrame; i++)
        {
            if (readPos != writePos)
            {
                // Replace microphone audio with injected audio
                pOutput[i] = m_pSharedData->audioBuffer[readPos];
                readPos = (readPos + 1) % bufferSize;
            }
            else
            {
                // No more injected audio, output silence
                pOutput[i] = 0.0f;
            }
        }

        m_pSharedData->readPos.store(readPos);

        // Check if playback finished
        if (readPos == writePos)
        {
            m_pSharedData->isPlaying.store(false);
        }
    }
    else
    {
        // Pass through microphone audio unchanged
        memcpy(pOutput, pInput, u32NumInputFrames * m_samplesPerFrame * sizeof(float));
    }
}

//-----------------------------------------------------------------------------
// Factory Implementation
//-----------------------------------------------------------------------------

STDMETHODIMP VoiceBoardAPOFactory::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;

    if (riid == IID_IUnknown || riid == IID_IClassFactory)
    {
        *ppv = static_cast<IClassFactory*>(this);
        return S_OK;
    }

    *ppv = NULL;
    return E_NOINTERFACE;
}

STDMETHODIMP_(ULONG) VoiceBoardAPOFactory::AddRef() { return 1; }
STDMETHODIMP_(ULONG) VoiceBoardAPOFactory::Release() { return 1; }

STDMETHODIMP VoiceBoardAPOFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv)
{
    if (pUnkOuter) return CLASS_E_NOAGGREGATION;
    if (!ppv) return E_POINTER;

    VoiceBoardAPO* pAPO = new (std::nothrow) VoiceBoardAPO();
    if (!pAPO) return E_OUTOFMEMORY;

    HRESULT hr = pAPO->QueryInterface(riid, ppv);
    pAPO->Release();

    return hr;
}

STDMETHODIMP VoiceBoardAPOFactory::LockServer(BOOL fLock)
{
    if (fLock)
        InterlockedIncrement(&g_serverLocks);
    else
        InterlockedDecrement(&g_serverLocks);
    return S_OK;
}
