#include "framework.h"

#include "ClassFactory.h"
#include "Module.h"
#include "Registry.h"

namespace {
LONG g_refCount = 0;
HINSTANCE g_instance = nullptr;
}

namespace Module {

void AddRef() noexcept { ::InterlockedIncrement(&g_refCount); }
void Release() noexcept { ::InterlockedDecrement(&g_refCount); }
long RefCount() noexcept { return ::InterlockedCompareExchange(&g_refCount, 0, 0); }

HINSTANCE Instance() noexcept { return g_instance; }
void SetInstance(HINSTANCE instance) noexcept { g_instance = instance; }

std::wstring Path() {
  std::wstring buffer(MAX_PATH, L'\0');
  for (;;) {
    const DWORD written =
        ::GetModuleFileNameW(g_instance, buffer.data(), static_cast<DWORD>(buffer.size()));
    if (written == 0) return {};
    if (written < buffer.size()) {
      buffer.resize(written);
      return buffer;
    }
    buffer.resize(buffer.size() * 2);
  }
}

std::wstring Directory() {
  const std::wstring path = Path();
  const size_t separator = path.find_last_of(L'\\');
  return separator == std::wstring::npos ? std::wstring{} : path.substr(0, separator);
}

const CLSID& Clsid() {
  static const CLSID clsid = [] {
    CLSID value{};
    ::CLSIDFromString(BRAND_CLSID_STR, &value);
    return value;
  }();
  return clsid;
}

}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID /*reserved*/) {
  if (reason == DLL_PROCESS_ATTACH) {
    Module::SetInstance(static_cast<HINSTANCE>(module));
    ::DisableThreadLibraryCalls(module);
  }
  return TRUE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv) {
  if (!ppv) return E_POINTER;
  *ppv = nullptr;
  if (!::IsEqualCLSID(rclsid, Module::Clsid())) return CLASS_E_CLASSNOTAVAILABLE;

  auto* factory = new (std::nothrow) ClassFactory();
  if (!factory) return E_OUTOFMEMORY;

  const HRESULT hr = factory->QueryInterface(riid, ppv);
  factory->Release();
  return hr;
}

STDAPI DllCanUnloadNow() { return Module::RefCount() == 0 ? S_OK : S_FALSE; }

STDAPI DllRegisterServer() { return Registry::Register(); }

STDAPI DllUnregisterServer() { return Registry::Unregister(); }
