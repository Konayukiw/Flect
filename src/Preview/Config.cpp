#include "Config.h"

namespace {

constexpr const wchar_t* kRootKey = L"Software\\Flect";
constexpr const wchar_t* kPreviewKey = L"Software\\Flect\\Preview";
constexpr const wchar_t* kThemeKey = L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
constexpr int kMinimumFontSize = 6;
constexpr int kMaximumFontSize = 72;

std::wstring ReadRaw(const wchar_t* subKey, const wchar_t* name, DWORD expected) {
  HKEY key = nullptr;
  if (::RegOpenKeyExW(HKEY_CURRENT_USER, subKey, 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) return {};
  DWORD type = 0;
  DWORD bytes = 0;
  LSTATUS status = ::RegQueryValueExW(key, name, nullptr, &type, nullptr, &bytes);
  if (status != ERROR_SUCCESS || type != expected || bytes < sizeof(wchar_t)) {
    ::RegCloseKey(key);
    return {};
  }
  std::wstring value(bytes / sizeof(wchar_t), L'\0');
  status = ::RegQueryValueExW(key, name, nullptr, nullptr, reinterpret_cast<BYTE*>(value.data()), &bytes);
  ::RegCloseKey(key);
  return status == ERROR_SUCCESS ? value : std::wstring{};
}

std::wstring ReadString(const wchar_t* subKey, const wchar_t* name) {
  std::wstring value = ReadRaw(subKey, name, REG_SZ);
  const size_t terminator = value.find(L'\0');
  if (terminator != std::wstring::npos) value.resize(terminator);
  return value;
}

bool ReadDword(const wchar_t* subKey, const wchar_t* name, DWORD& value) {
  DWORD size = sizeof(value);
  return ::RegGetValueW(HKEY_CURRENT_USER, subKey, name, RRF_RT_REG_DWORD, nullptr, &value, &size) == ERROR_SUCCESS;
}

std::vector<std::wstring> ReadList(const wchar_t* subKey, const wchar_t* name) {
  std::vector<std::wstring> items;
  const std::wstring raw = ReadRaw(subKey, name, REG_MULTI_SZ);
  size_t start = 0;
  while (start < raw.size()) {
    const size_t end = raw.find(L'\0', start);
    const size_t stop = end == std::wstring::npos ? raw.size() : end;
    if (stop > start) items.push_back(raw.substr(start, stop - start));
    start = stop + 1;
  }
  return items;
}

std::wstring ResolveLocale(const std::wstring& setting) {
  if (setting == L"ja") return L"ja";
  if (setting == L"zh-Hans") return L"zh";
  if (setting == L"en") return L"en";
  const LANGID language = PRIMARYLANGID(::GetUserDefaultUILanguage());
  if (language == LANG_JAPANESE) return L"ja";
  if (language == LANG_CHINESE) return L"zh";
  return L"en";
}

}

PreviewConfig Config::Read() {
  PreviewConfig config;

  const std::wstring family = ReadString(kPreviewKey, L"FontFamily");
  if (!family.empty()) config.fontFamily = family;

  DWORD size = 0;
  if (ReadDword(kPreviewKey, L"FontSize", size)) config.fontSize = std::clamp(static_cast<int>(size), kMinimumFontSize, kMaximumFontSize);

  DWORD wrap = 0;
  if (ReadDword(kPreviewKey, L"WordWrap", wrap)) config.wordWrap = wrap != 0;

  config.sampleText = ReadString(kPreviewKey, L"SampleText");
  config.trustedFolders = ReadList(kPreviewKey, L"TrustedFolders");
  config.locale = ResolveLocale(ReadString(kRootKey, L"Language"));

  DWORD light = 1;
  ReadDword(kThemeKey, L"AppsUseLightTheme", light);
  config.darkTheme = light == 0;
  return config;
}
