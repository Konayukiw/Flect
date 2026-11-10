#include "Registry.h"

#include "Module.h"

namespace {

constexpr const wchar_t* kClasses = L"Software\\Classes\\";
constexpr const wchar_t* kPreviewHandlers = L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers";
constexpr const wchar_t* kSurrogate = L"%SystemRoot%\\system32\\prevhost.exe";
constexpr const wchar_t* kUntrustedValue = L"AutomaticallyPreviewUntrustedFiles";

struct HandlerClass {
  const wchar_t* clsid;
  const wchar_t* suffix;
  DWORD untrusted;
};

constexpr HandlerClass kClassesToRegister[] = {
    {BRAND_PREVIEW_CLSID_STR, L" Preview", 1},
    {BRAND_PREVIEW_HTML_CLSID_STR, L" HTML Preview", 0},
};

std::wstring ClsidKey(const wchar_t* clsid) { return std::wstring(kClasses) + L"CLSID\\" + clsid; }

std::wstring AppIdKey() { return std::wstring(kClasses) + L"AppID\\" + BRAND_PREVIEW_APPID_STR; }

LSTATUS Write(const std::wstring& subKey, const wchar_t* name, DWORD type, const std::wstring& value) {
  HKEY key = nullptr;
  LSTATUS status = ::RegCreateKeyExW(HKEY_CURRENT_USER, subKey.c_str(), 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_WRITE,
                                     nullptr, &key, nullptr);
  if (status != ERROR_SUCCESS) return status;
  status = ::RegSetValueExW(key, name, 0, type, reinterpret_cast<const BYTE*>(value.c_str()),
                            static_cast<DWORD>((value.size() + 1) * sizeof(wchar_t)));
  ::RegCloseKey(key);
  return status;
}

LSTATUS WriteNumber(const std::wstring& subKey, const wchar_t* name, DWORD value) {
  HKEY key = nullptr;
  LSTATUS status = ::RegCreateKeyExW(HKEY_CURRENT_USER, subKey.c_str(), 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_WRITE,
                                     nullptr, &key, nullptr);
  if (status != ERROR_SUCCESS) return status;
  status = ::RegSetValueExW(key, name, 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
  ::RegCloseKey(key);
  return status;
}

LSTATUS RegisterHandler(const HandlerClass& handler, const std::wstring& dll) {
  const std::wstring display = std::wstring(BRAND_NAME) + handler.suffix;
  const std::wstring clsid = ClsidKey(handler.clsid);
  const std::wstring server = clsid + L"\\InprocServer32";

  LSTATUS status = Write(clsid, nullptr, REG_SZ, display);
  if (status == ERROR_SUCCESS) status = Write(clsid, L"AppID", REG_SZ, BRAND_PREVIEW_APPID_STR);
  if (status == ERROR_SUCCESS) status = Write(clsid, L"DisplayName", REG_SZ, display);
  if (status == ERROR_SUCCESS) status = WriteNumber(clsid, kUntrustedValue, handler.untrusted);
  if (status == ERROR_SUCCESS) status = Write(server, nullptr, REG_SZ, dll);
  if (status == ERROR_SUCCESS) status = Write(server, L"ThreadingModel", REG_SZ, L"Apartment");
  if (status == ERROR_SUCCESS) status = Write(kPreviewHandlers, handler.clsid, REG_SZ, display);
  return status;
}

}

HRESULT Registry::Register() {
  const std::wstring dll = Module::Path();
  if (dll.empty()) return E_FAIL;

  LSTATUS status = Write(AppIdKey(), nullptr, REG_SZ, std::wstring(BRAND_NAME) + L" Preview Host");
  if (status == ERROR_SUCCESS) status = Write(AppIdKey(), L"DllSurrogate", REG_EXPAND_SZ, kSurrogate);
  for (const HandlerClass& handler : kClassesToRegister) {
    if (status == ERROR_SUCCESS) status = RegisterHandler(handler, dll);
  }
  if (status != ERROR_SUCCESS) return HRESULT_FROM_WIN32(status);

  ::SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, nullptr, nullptr);
  return S_OK;
}

HRESULT Registry::Unregister() {
  LSTATUS worst = ERROR_SUCCESS;

  HKEY handlers = nullptr;
  if (::RegOpenKeyExW(HKEY_CURRENT_USER, kPreviewHandlers, 0, KEY_SET_VALUE, &handlers) == ERROR_SUCCESS) {
    for (const HandlerClass& handler : kClassesToRegister) {
      const LSTATUS status = ::RegDeleteValueW(handlers, handler.clsid);
      if (status != ERROR_SUCCESS && status != ERROR_FILE_NOT_FOUND) worst = status;
    }
    ::RegCloseKey(handlers);
  }

  std::vector<std::wstring> keys;
  for (const HandlerClass& handler : kClassesToRegister) keys.push_back(ClsidKey(handler.clsid));
  keys.push_back(AppIdKey());
  for (const std::wstring& key : keys) {
    const LSTATUS status = ::RegDeleteTreeW(HKEY_CURRENT_USER, key.c_str());
    if (status != ERROR_SUCCESS && status != ERROR_FILE_NOT_FOUND) worst = status;
  }

  ::SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, nullptr, nullptr);
  return worst == ERROR_SUCCESS ? S_OK : HRESULT_FROM_WIN32(worst);
}
