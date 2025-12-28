#include "VoiceBoardAPO.h"
#include <olectl.h>
#include <strsafe.h>

// Registry keys for APO registration
static const WCHAR* APO_REG_KEY = L"SOFTWARE\\Classes\\CLSID\\{8A5BC3E7-9D4F-4B2A-8C1E-6F3D5A7B9C0E}";
static const WCHAR* APO_INPROC_KEY = L"SOFTWARE\\Classes\\CLSID\\{8A5BC3E7-9D4F-4B2A-8C1E-6F3D5A7B9C0E}\\InprocServer32";

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

// Standard COM DLL exports
STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    if (!ppv) return E_POINTER;

    if (rclsid == CLSID_VoiceBoardAPO)
    {
        return g_Factory.QueryInterface(riid, ppv);
    }

    *ppv = NULL;
    return CLASS_E_CLASSNOTAVAILABLE;
}

STDAPI DllCanUnloadNow()
{
    return (g_serverLocks == 0) ? S_OK : S_FALSE;
}

STDAPI DllRegisterServer()
{
    HRESULT hr = S_OK;
    WCHAR szModulePath[MAX_PATH];
    HKEY hKey = NULL;
    HKEY hSubKey = NULL;

    // Get DLL path
    if (!GetModuleFileNameW(g_hModule, szModulePath, MAX_PATH))
        return HRESULT_FROM_WIN32(GetLastError());

    // Register CLSID
    LONG result = RegCreateKeyExW(HKEY_LOCAL_MACHINE, APO_REG_KEY,
        0, NULL, 0, KEY_WRITE, NULL, &hKey, NULL);
    if (result != ERROR_SUCCESS)
        return HRESULT_FROM_WIN32(result);

    RegSetValueExW(hKey, NULL, 0, REG_SZ, (BYTE*)L"VoiceBoard APO", sizeof(L"VoiceBoard APO"));
    RegCloseKey(hKey);

    // Register InprocServer32
    result = RegCreateKeyExW(HKEY_LOCAL_MACHINE, APO_INPROC_KEY,
        0, NULL, 0, KEY_WRITE, NULL, &hSubKey, NULL);
    if (result != ERROR_SUCCESS)
        return HRESULT_FROM_WIN32(result);

    RegSetValueExW(hSubKey, NULL, 0, REG_SZ, (BYTE*)szModulePath, (DWORD)(wcslen(szModulePath) + 1) * sizeof(WCHAR));
    RegSetValueExW(hSubKey, L"ThreadingModel", 0, REG_SZ, (BYTE*)L"Both", sizeof(L"Both"));
    RegCloseKey(hSubKey);

    // Register as Audio Processing Object
    HKEY hAudioKey = NULL;
    result = RegCreateKeyExW(HKEY_LOCAL_MACHINE,
        L"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Audio\\AudioProcessingObjects\\{8A5BC3E7-9D4F-4B2A-8C1E-6F3D5A7B9C0E}",
        0, NULL, 0, KEY_WRITE, NULL, &hAudioKey, NULL);
    if (result == ERROR_SUCCESS)
    {
        RegSetValueExW(hAudioKey, L"FriendlyName", 0, REG_SZ, (BYTE*)L"VoiceBoard APO", sizeof(L"VoiceBoard APO"));

        DWORD dwFlags = APO_FLAG_DEFAULT;
        RegSetValueExW(hAudioKey, L"Flags", 0, REG_DWORD, (BYTE*)&dwFlags, sizeof(DWORD));

        DWORD dwVersion = 0x00010000;
        RegSetValueExW(hAudioKey, L"Version", 0, REG_DWORD, (BYTE*)&dwVersion, sizeof(DWORD));

        RegCloseKey(hAudioKey);
    }

    return hr;
}

STDAPI DllUnregisterServer()
{
    RegDeleteTreeW(HKEY_LOCAL_MACHINE, APO_REG_KEY);
    RegDeleteTreeW(HKEY_LOCAL_MACHINE,
        L"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Audio\\AudioProcessingObjects\\{8A5BC3E7-9D4F-4B2A-8C1E-6F3D5A7B9C0E}");
    return S_OK;
}
