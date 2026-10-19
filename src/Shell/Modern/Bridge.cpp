#include "Bridge.h"

#include <sstream>

namespace {
std::wstring OwnPath() {
  HMODULE module = nullptr;
  if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                            GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                        reinterpret_cast<LPCWSTR>(&OwnPath), &module)) return {};
  std::wstring path(32768, L'\0');
  const DWORD length = GetModuleFileNameW(module, path.data(), static_cast<DWORD>(path.size()));
  if (!length || length >= path.size()) return {};
  path.resize(length);
  return path;
}

std::wstring Quote(const std::wstring& value) {
  std::wstring result = L"\"";
  size_t slashes = 0;
  for (wchar_t c : value) {
    if (c == L'\\') { ++slashes; continue; }
    result.append(slashes * (c == L'"' ? 2 : 1), L'\\');
    if (c == L'"') result += L'\\';
    result += c;
    slashes = 0;
  }
  result.append(slashes * 2, L'\\');
  return result + L'"';
}

HRESULT StageSelection(const std::vector<std::wstring>& paths, std::wstring& file) {
  wchar_t temp[32768]{};
  DWORD length = GetTempPathW(ARRAYSIZE(temp), temp);
  if (!length || length >= ARRAYSIZE(temp)) return E_FAIL;
  const std::wstring directory = std::wstring(temp) + L"FlectModernMenu";
  if (!CreateDirectoryW(directory.c_str(), nullptr) && GetLastError() != ERROR_ALREADY_EXISTS)
    return HRESULT_FROM_WIN32(GetLastError());
  GUID guid{};
  HRESULT hr = CoCreateGuid(&guid);
  if (FAILED(hr)) return hr;
  wchar_t token[40]{};
  StringFromGUID2(guid, token, ARRAYSIZE(token));
  file = directory + L"\\selection-" + token + L".txt";
  std::wstring text;
  for (const auto& path : paths) text += path + L"\r\n";
  const int bytes = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(),
      static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
  if (!bytes) return HRESULT_FROM_WIN32(GetLastError());
  if (bytes > 16 * 1024 * 1024) return E_INVALIDARG;
  std::string utf8(static_cast<size_t>(bytes), '\0');
  WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(),
      static_cast<int>(text.size()), utf8.data(), bytes, nullptr, nullptr);
  HANDLE handle = CreateFileW(file.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW,
                              FILE_ATTRIBUTE_TEMPORARY, nullptr);
  if (handle == INVALID_HANDLE_VALUE) return HRESULT_FROM_WIN32(GetLastError());
  DWORD written = 0;
  BOOL ok = WriteFile(handle, utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr);
  DWORD error = GetLastError();
  CloseHandle(handle);
  if (!ok || written != utf8.size()) {
    DeleteFileW(file.c_str());
    return HRESULT_FROM_WIN32(ok ? ERROR_WRITE_FAULT : error);
  }
  return S_OK;
}
}

std::wstring Bridge::OwnDirectory() {
  const std::wstring path = OwnPath();
  return path.substr(0, path.find_last_of(L'\\'));
}

std::wstring Bridge::AppDirectory() {
  wchar_t buffer[32768]{};
  DWORD bytes = sizeof(buffer);
  if (RegGetValueW(HKEY_CURRENT_USER,
      BRAND_UNINSTALL_KEY,
      L"InstallLocation", RRF_RT_REG_SZ, nullptr, buffer, &bytes) == ERROR_SUCCESS) {
    std::wstring path(buffer);
    while (!path.empty() && path.back() == L'\\') path.pop_back();
    if (GetFileAttributesW((path + L"\\" + BRAND_APP_EXE).c_str()) != INVALID_FILE_ATTRIBUTES) return path;
  }
  PWSTR local = nullptr;
  if (FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &local))) return {};
  std::wstring path = std::wstring(local) + L"\\" + BRAND_NAME;
  CoTaskMemFree(local);
  return path;
}

SelectionInfo Bridge::Selection(IShellItemArray* items) {
  if (!items) return {};
  Microsoft::WRL::ComPtr<IDataObject> data;
  if (FAILED(items->BindToHandler(nullptr, BHID_DataObject, IID_PPV_ARGS(&data)))) return {};
  return Sel::FromDataObject(data.Get());
}

HRESULT Bridge::ShellItems(const std::vector<std::wstring>& paths, IShellItemArray** items) {
  if (!items) return E_POINTER;
  *items = nullptr;
  if (paths.empty() || paths.size() > 20000) return E_INVALIDARG;
  std::vector<PIDLIST_ABSOLUTE> pidls;
  HRESULT hr = S_OK;
  for (const auto& path : paths) {
    PIDLIST_ABSOLUTE pidl = nullptr;
    hr = SHParseDisplayName(path.c_str(), nullptr, &pidl, 0, nullptr);
    if (FAILED(hr)) break;
    pidls.push_back(pidl);
  }
  if (SUCCEEDED(hr)) hr = SHCreateShellItemArrayFromIDLists(static_cast<UINT>(pidls.size()),
      const_cast<PCIDLIST_ABSOLUTE*>(pidls.data()), items);
  for (auto pidl : pidls) CoTaskMemFree(pidl);
  return hr;
}

HRESULT Bridge::StartMenu(const std::vector<std::wstring>& paths, const std::wstring& menu) {
  std::wstring file;
  HRESULT hr = StageSelection(paths, file);
  if (FAILED(hr)) return hr;
  const std::wstring executable = OwnDirectory() + L"\\FlectMenu.exe";
  std::wstring args = Quote(executable) + L" --input " + Quote(file) + L" --menu " + Quote(menu);
  STARTUPINFOW startup{sizeof(startup)};
  PROCESS_INFORMATION process{};
  if (!CreateProcessW(executable.c_str(), args.data(), nullptr, nullptr, FALSE,
       CREATE_UNICODE_ENVIRONMENT, nullptr, OwnDirectory().c_str(), &startup, &process)) {
    const DWORD error = GetLastError();
    DeleteFileW(file.c_str());
    return HRESULT_FROM_WIN32(error);
  }
  AllowSetForegroundWindow(process.dwProcessId);
  CloseHandle(process.hThread);
  CloseHandle(process.hProcess);
  return S_OK;
}

HRESULT Bridge::ReadSelection(const std::wstring& file, std::vector<std::wstring>& paths) {
  wchar_t temp[32768]{};
  if (!GetTempPathW(ARRAYSIZE(temp), temp)) return E_FAIL;
  const std::wstring prefix = std::wstring(temp) + L"FlectModernMenu\\selection-";
  if (file.compare(0, prefix.size(), prefix) != 0 || file.find(L"..") != std::wstring::npos)
    return E_INVALIDARG;
  HANDLE handle = CreateFileW(file.c_str(), GENERIC_READ | DELETE, 0, nullptr, OPEN_EXISTING,
                              FILE_FLAG_DELETE_ON_CLOSE, nullptr);
  if (handle == INVALID_HANDLE_VALUE) return HRESULT_FROM_WIN32(GetLastError());
  LARGE_INTEGER size{};
  if (!GetFileSizeEx(handle, &size) || size.QuadPart <= 0 || size.QuadPart > 16 * 1024 * 1024) {
    CloseHandle(handle);
    return E_INVALIDARG;
  }
  std::string utf8(static_cast<size_t>(size.QuadPart), '\0');
  DWORD read = 0;
  BOOL ok = ReadFile(handle, utf8.data(), static_cast<DWORD>(utf8.size()), &read, nullptr);
  CloseHandle(handle);
  if (!ok || read != utf8.size()) return E_FAIL;
  int count = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, utf8.data(),
      static_cast<int>(utf8.size()), nullptr, 0);
  if (!count) return E_INVALIDARG;
  std::wstring text(static_cast<size_t>(count), L'\0');
  MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, utf8.data(),
      static_cast<int>(utf8.size()), text.data(), count);
  std::wistringstream input(text);
  std::wstring line;
  while (std::getline(input, line)) {
    if (!line.empty() && line.back() == L'\r') line.pop_back();
    if (!line.empty()) paths.push_back(std::move(line));
    if (paths.size() > 20000) return E_INVALIDARG;
  }
  return paths.empty() ? E_INVALIDARG : S_OK;
}

namespace Module {
std::wstring Directory() { return Bridge::AppDirectory(); }
}
