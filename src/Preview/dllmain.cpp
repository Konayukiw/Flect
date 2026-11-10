#include "framework.h"

#include "Module.h"
#include "PreviewHandler.h"
#include "Registry.h"
#include "Sniff.h"
#include "Text.h"

namespace {

LONG g_refCount = 0;
HINSTANCE g_instance = nullptr;

class ClassFactory final : public IClassFactory {
 public:
  IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override {
    if (!ppv) return E_POINTER;
    if (riid == IID_IUnknown || riid == IID_IClassFactory) {
      *ppv = static_cast<IClassFactory*>(this);
      AddRef();
      return S_OK;
    }
    *ppv = nullptr;
    return E_NOINTERFACE;
  }

  IFACEMETHODIMP_(ULONG) AddRef() override { return 2; }
  IFACEMETHODIMP_(ULONG) Release() override { return 1; }

  IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override {
    if (!ppv) return E_POINTER;
    *ppv = nullptr;
    if (outer) return CLASS_E_NOAGGREGATION;
    auto* handler = new (std::nothrow) PreviewHandler();
    if (!handler) return E_OUTOFMEMORY;
    const HRESULT hr = handler->QueryInterface(riid, ppv);
    handler->Release();
    return hr;
  }

  IFACEMETHODIMP LockServer(BOOL lock) override {
    if (lock) Module::AddRef();
    else Module::Release();
    return S_OK;
  }
};

ClassFactory g_factory;

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
    const DWORD written = ::GetModuleFileNameW(g_instance, buffer.data(), static_cast<DWORD>(buffer.size()));
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
    ::CLSIDFromString(BRAND_PREVIEW_CLSID_STR, &value);
    return value;
  }();
  return clsid;
}

const CLSID& HtmlClsid() {
  static const CLSID clsid = [] {
    CLSID value{};
    ::CLSIDFromString(BRAND_PREVIEW_HTML_CLSID_STR, &value);
    return value;
  }();
  return clsid;
}

}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID) {
  if (reason == DLL_PROCESS_ATTACH) Module::SetInstance(static_cast<HINSTANCE>(module));
  return TRUE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv) {
  if (!ppv) return E_POINTER;
  *ppv = nullptr;
  if (!::IsEqualCLSID(rclsid, Module::Clsid()) && !::IsEqualCLSID(rclsid, Module::HtmlClsid())) return CLASS_E_CLASSNOTAVAILABLE;
  return g_factory.QueryInterface(riid, ppv);
}

STDAPI DllCanUnloadNow() { return Module::RefCount() == 0 ? S_OK : S_FALSE; }

STDAPI DllRegisterServer() { return Registry::Register(); }

STDAPI DllUnregisterServer() { return Registry::Unregister(); }

extern "C" __declspec(dllexport) int __stdcall FlectPreviewSniff(const uint8_t* data, size_t size, const wchar_t* name,
                                                                wchar_t* output, size_t capacity) {
  if (!output || capacity == 0) return 0;
  const Sniff::Result result = Sniff::Of(std::span<const uint8_t>(data, size), name ? name : L"");
  const std::wstring text = result.name + L"|" + result.family + L"|" + result.demuxer;
  return SUCCEEDED(::StringCchCopyW(output, capacity, text.c_str())) ? 1 : 0;
}
