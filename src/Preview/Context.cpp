#include "Context.h"

#include <set>

#include "Text.h"

namespace {

constexpr size_t kHeaderBytes = 8192;
constexpr int kRootSearchDepth = 16;
constexpr const wchar_t* kDocSuffix = L".flect-preview.invalid";

SRWLOCK g_consentLock = SRWLOCK_INIT;
std::set<std::wstring> g_scriptConsent;

bool Exists(const std::wstring& path) { return ::GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES; }

std::wstring ParentOf(const std::wstring& path) {
  const size_t separator = path.find_last_of(L'\\');
  return separator == std::wstring::npos ? std::wstring{} : path.substr(0, separator);
}

std::wstring FindRoot(const std::wstring& path) {
  const std::wstring start = ParentOf(path);
  std::wstring folder = start;
  for (int depth = 0; depth < kRootSearchDepth && !folder.empty(); ++depth) {
    if (Exists(folder + L"\\.git") || Exists(folder + L"\\package.json")) return folder;
    const std::wstring parent = ParentOf(folder);
    if (parent.empty() || parent.size() >= folder.size() || parent.back() == L':' ) break;
    folder = parent;
  }
  return start;
}

std::wstring RelativeTo(const std::wstring& root, const std::wstring& path) {
  std::wstring relative = path.substr(root.size() + 1);
  std::replace(relative.begin(), relative.end(), L'\\', L'/');
  return relative;
}

std::vector<uint8_t> ReadHeader(const std::wstring& path) {
  std::vector<uint8_t> buffer(kHeaderBytes);
  HANDLE file = ::CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING,
                              FILE_ATTRIBUTE_NORMAL, nullptr);
  if (file == INVALID_HANDLE_VALUE) return {};
  DWORD read = 0;
  const BOOL ok = ::ReadFile(file, buffer.data(), static_cast<DWORD>(buffer.size()), &read, nullptr);
  ::CloseHandle(file);
  buffer.resize(ok ? read : 0);
  return buffer;
}

bool IsMedia(const std::wstring& family) { return family == L"video" || family == L"audio"; }

void Classify(PreviewContext& context) {
  const Sniff::Result& sniff = context.sniff;
  if (context.size == 0) return;

  if ((Text::SameFold(context.extension, L".ts") || Text::SameFold(context.extension, L".mts")) && sniff.name == L"MPEG-TS") {
    context.type = FileType{Kind::Video, L""};
    return;
  }

  switch (context.type.kind) {
    case Kind::Text:
    case Kind::Html:
    case Kind::Markdown:
      if (sniff.family != L"text" && sniff.name != L"SVG") {
        context.binary = true;
        context.mismatch = sniff.name;
      }
      break;
    case Kind::Svg:
      if (sniff.family != L"text" && sniff.name != L"SVG") {
        context.binary = true;
        context.mismatch = sniff.name;
      }
      break;
    case Kind::Pdf:
      if (sniff.family != L"pdf") context.mismatch = sniff.name.empty() ? L"UNKNOWN" : sniff.name;
      break;
    case Kind::Font:
      if (sniff.family != L"font") context.mismatch = sniff.name.empty() ? L"UNKNOWN" : sniff.name;
      break;
    case Kind::Video:
    case Kind::Audio:
      if (!IsMedia(sniff.family) && sniff.family != L"binary") context.mismatch = sniff.name;
      break;
    default:
      break;
  }
}

}

std::wstring Context::DataFolder() {
  PWSTR raw = nullptr;
  std::wstring folder;
  if (SUCCEEDED(::SHGetKnownFolderPath(FOLDERID_LocalAppDataLow, 0, nullptr, &raw))) folder = raw;
  if (raw) ::CoTaskMemFree(raw);
  if (folder.empty()) return {};
  folder += L"\\Flect";
  ::CreateDirectoryW(folder.c_str(), nullptr);
  folder += L"\\Preview";
  ::CreateDirectoryW(folder.c_str(), nullptr);
  return folder;
}

bool Context::HasScriptConsent(const std::wstring& path) {
  ::AcquireSRWLockShared(&g_consentLock);
  const bool granted = g_scriptConsent.count(Text::Lower(path)) != 0;
  ::ReleaseSRWLockShared(&g_consentLock);
  return granted;
}

void Context::GrantScriptConsent(const std::wstring& path) {
  ::AcquireSRWLockExclusive(&g_consentLock);
  g_scriptConsent.insert(Text::Lower(path));
  ::ReleaseSRWLockExclusive(&g_consentLock);
}

bool Context::Build(const std::wstring& path, PreviewContext& context) {
  WIN32_FILE_ATTRIBUTE_DATA data{};
  if (!::GetFileAttributesExW(path.c_str(), GetFileExInfoStandard, &data)) return false;
  if (data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) return false;

  context.path = path;
  context.size = static_cast<uint64_t>(data.nFileSizeHigh) << 32 | data.nFileSizeLow;
  const size_t separator = path.find_last_of(L'\\');
  context.name = separator == std::wstring::npos ? path : path.substr(separator + 1);
  context.extension = ::PathFindExtensionW(context.name.c_str());
  context.type = Kinds::Lookup(context.extension);
  if (context.type.kind == Kind::None) return false;

  context.config = Config::Read();
  context.trust = Trust::Evaluate(path, context.config.trustedFolders);
  context.root = FindRoot(path);
  context.relative = RelativeTo(context.root, path);
  context.docHost = L"d" + Text::RandomHex(12) + kDocSuffix;

  const std::vector<uint8_t> header = ReadHeader(path);
  context.sniff = Sniff::Of(header, context.name);
  Classify(context);

  if (context.trust.level == Trust::Level::Trusted) {
    context.allow.relative = true;
    context.allow.scripts = true;
  } else if (context.trust.level == Trust::Level::Normal) {
    context.allow.scripts = HasScriptConsent(path);
  }
  return true;
}
