#include "Registry.h"

#include "Module.h"

namespace {

constexpr const wchar_t* kClassesRoot = L"Software\\Classes\\";

constexpr const wchar_t* kScopes[] = {L"*", L"Directory"};

std::wstring ClsidKey() {
  return std::wstring(kClassesRoot) + L"CLSID\\" + BRAND_CLSID_STR;
}

std::wstring HandlerKey(const wchar_t* scope) {
  return std::wstring(kClassesRoot) + scope + L"\\shellex\\ContextMenuHandlers\\" + BRAND_NAME;
}

LSTATUS WriteString(const std::wstring& subKey, const wchar_t* valueName,
                    const std::wstring& value) {
  HKEY key = nullptr;
  LSTATUS status = ::RegCreateKeyExW(HKEY_CURRENT_USER, subKey.c_str(), 0, nullptr,
                                     REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr, &key,
                                     nullptr);
  if (status != ERROR_SUCCESS) return status;

  status = ::RegSetValueExW(
      key, valueName, 0, REG_SZ, reinterpret_cast<const BYTE*>(value.c_str()),
      static_cast<DWORD>((value.size() + 1) * sizeof(wchar_t)));
  ::RegCloseKey(key);
  return status;
}

}

HRESULT Registry::Register() {
  const std::wstring dll = Module::Path();
  if (dll.empty()) return E_FAIL;

  const std::wstring clsid = ClsidKey();
  const std::wstring server = clsid + L"\\InprocServer32";

  LSTATUS status = WriteString(clsid, nullptr, std::wstring(BRAND_NAME) + L" shell extension");
  if (status == ERROR_SUCCESS) status = WriteString(server, nullptr, dll);
  if (status == ERROR_SUCCESS) status = WriteString(server, L"ThreadingModel", L"Apartment");

  for (const wchar_t* scope : kScopes) {
    if (status != ERROR_SUCCESS) break;
    status = WriteString(HandlerKey(scope), nullptr, BRAND_CLSID_STR);
  }

  if (status != ERROR_SUCCESS) return HRESULT_FROM_WIN32(status);

  ::SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, nullptr, nullptr);
  return S_OK;
}

HRESULT Registry::Unregister() {
  LSTATUS worst = ERROR_SUCCESS;

  for (const wchar_t* scope : kScopes) {
    const LSTATUS status =
        ::RegDeleteTreeW(HKEY_CURRENT_USER, HandlerKey(scope).c_str());
    if (status != ERROR_SUCCESS && status != ERROR_FILE_NOT_FOUND) worst = status;
  }

  const LSTATUS status = ::RegDeleteTreeW(HKEY_CURRENT_USER, ClsidKey().c_str());
  if (status != ERROR_SUCCESS && status != ERROR_FILE_NOT_FOUND) worst = status;

  ::SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, nullptr, nullptr);
  return worst == ERROR_SUCCESS ? S_OK : HRESULT_FROM_WIN32(worst);
}
