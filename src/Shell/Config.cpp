#include "Config.h"

namespace {

constexpr const wchar_t* kRoot = L"Software\\Flect";
constexpr const wchar_t* kMenuKey = L"Software\\Flect\\Menu";
constexpr const wchar_t* kPresetKey = L"Software\\Flect\\Presets";

constexpr ULONGLONG kCacheTtlMs = 1000;

std::wstring ReadString(const wchar_t* subKey, const wchar_t* valueName) {
  HKEY key = nullptr;
  if (::RegOpenKeyExW(HKEY_CURRENT_USER, subKey, 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) {
    return {};
  }

  DWORD type = 0;
  DWORD bytes = 0;
  LSTATUS status = ::RegQueryValueExW(key, valueName, nullptr, &type, nullptr, &bytes);
  if (status != ERROR_SUCCESS || type != REG_SZ || bytes < sizeof(wchar_t)) {
    ::RegCloseKey(key);
    return {};
  }

  std::wstring value(bytes / sizeof(wchar_t), L'\0');
  status = ::RegQueryValueExW(key, valueName, nullptr, nullptr,
                              reinterpret_cast<BYTE*>(value.data()), &bytes);
  ::RegCloseKey(key);
  if (status != ERROR_SUCCESS) return {};

  const size_t terminator = value.find(L'\0');
  if (terminator != std::wstring::npos) value.resize(terminator);
  return value;
}

void SplitHidden(const std::wstring& list, std::set<std::wstring>& into) {
  size_t start = 0;
  while (start <= list.size()) {
    const size_t separator = list.find(L';', start);
    const size_t end = separator == std::wstring::npos ? list.size() : separator;
    if (end > start) into.insert(list.substr(start, end - start));
    if (separator == std::wstring::npos) break;
    start = separator + 1;
  }
}

void ReadPresets(std::vector<std::pair<std::wstring, std::wstring>>& into) {
  HKEY key = nullptr;
  if (::RegOpenKeyExW(HKEY_CURRENT_USER, kPresetKey, 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) {
    return;
  }

  for (DWORD index = 0;; ++index) {
    wchar_t name[256];
    DWORD nameLength = ARRAYSIZE(name);
    DWORD type = 0;
    wchar_t data[128];
    DWORD dataBytes = sizeof(data);

    const LSTATUS status =
        ::RegEnumValueW(key, index, name, &nameLength, nullptr, &type,
                        reinterpret_cast<BYTE*>(data), &dataBytes);
    if (status == ERROR_NO_MORE_ITEMS) break;
    if (status != ERROR_SUCCESS) continue;
    if (type != REG_SZ || dataBytes < sizeof(wchar_t)) continue;

    std::wstring value(data, dataBytes / sizeof(wchar_t));
    const size_t terminator = value.find(L'\0');
    if (terminator != std::wstring::npos) value.resize(terminator);
    into.emplace_back(std::wstring(name, nameLength), std::move(value));
  }

  ::RegCloseKey(key);
}

ShellConfig ReadFromRegistry() {
  ShellConfig config;

  config.language = ReadString(kRoot, L"Language");
  if (config.language.empty()) config.language = L"system";

  SplitHidden(ReadString(kMenuKey, L"Hidden"), config.hidden);
  ReadPresets(config.presets);
  return config;
}

SRWLOCK g_lock = SRWLOCK_INIT;
ShellConfig g_cache;
ULONGLONG g_stamp = 0;
bool g_loaded = false;

}

ShellConfig Config::Get() {
  ::AcquireSRWLockExclusive(&g_lock);
  const ULONGLONG now = ::GetTickCount64();
  if (!g_loaded || now - g_stamp > kCacheTtlMs) {
    g_cache = ReadFromRegistry();
    g_stamp = now;
    g_loaded = true;
  }
  ShellConfig copy = g_cache;
  ::ReleaseSRWLockExclusive(&g_lock);
  return copy;
}
