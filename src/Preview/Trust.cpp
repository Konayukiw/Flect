#include "Trust.h"

#include "Text.h"

using Microsoft::WRL::ComPtr;

namespace {

enum class Mark { Absent, Present, Unknown };

bool Untrusted(int zone) { return zone < URLZONE_LOCAL_MACHINE || zone > URLZONE_TRUSTED; }

Mark ReadMark(const std::wstring& path, int& zone) {
  const std::wstring stream = path + L":Zone.Identifier";
  HANDLE file = ::CreateFileW(stream.c_str(), FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr,
                              OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
  if (file == INVALID_HANDLE_VALUE) {
    const DWORD error = ::GetLastError();
    return error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND ? Mark::Absent : Mark::Unknown;
  }
  ::CloseHandle(file);

  ComPtr<IPersistFile> persist;
  if (FAILED(::CoCreateInstance(CLSID_PersistentZoneIdentifier, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&persist)))) return Mark::Unknown;
  if (FAILED(persist->Load(path.c_str(), STGM_READ))) return Mark::Unknown;
  ComPtr<IZoneIdentifier> identifier;
  if (FAILED(persist.As(&identifier))) return Mark::Unknown;
  DWORD id = 0;
  if (FAILED(identifier->GetId(&id))) return Mark::Unknown;
  zone = static_cast<int>(id);
  return Mark::Present;
}

bool MapZone(const std::wstring& path, int& zone) {
  ComPtr<IInternetSecurityManager> manager;
  if (FAILED(::CoCreateInstance(CLSID_InternetSecurityManager, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&manager)))) return false;
  DWORD mapped = 0;
  if (FAILED(manager->MapUrlToZone(path.c_str(), &mapped, 0))) return false;
  zone = static_cast<int>(mapped);
  return true;
}

bool Inside(const std::wstring& file, const std::wstring& folder) {
  if (folder.empty() || file.size() <= folder.size()) return false;
  if (!Text::SameFold(std::wstring_view(file).substr(0, folder.size()), folder)) return false;
  return folder.back() == L'\\' || file[folder.size()] == L'\\';
}

}

std::wstring Trust::FinalPath(const std::wstring& path) {
  HANDLE file = ::CreateFileW(path.c_str(), FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr,
                              OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, nullptr);
  if (file == INVALID_HANDLE_VALUE) return {};
  std::wstring buffer(1024, L'\0');
  DWORD written = ::GetFinalPathNameByHandleW(file, buffer.data(), static_cast<DWORD>(buffer.size()), FILE_NAME_NORMALIZED);
  if (written >= buffer.size()) {
    buffer.resize(written + 1);
    written = ::GetFinalPathNameByHandleW(file, buffer.data(), static_cast<DWORD>(buffer.size()), FILE_NAME_NORMALIZED);
  }
  ::CloseHandle(file);
  if (written == 0 || written >= buffer.size()) return {};
  buffer.resize(written);
  return buffer;
}

Trust::Result Trust::Evaluate(const std::wstring& path, const std::vector<std::wstring>& trustedFolders) {
  Result result;

  int markedZone = -1;
  const Mark mark = ReadMark(path, markedZone);
  if (mark == Mark::Unknown) return result;
  result.marked = mark == Mark::Present && Untrusted(markedZone);
  if (result.marked) {
    result.zone = markedZone;
    return result;
  }

  int mappedZone = -1;
  if (!MapZone(path, mappedZone)) return result;

  result.zone = std::max(mark == Mark::Present ? markedZone : 0, mappedZone);
  if (Untrusted(result.zone)) return result;

  result.level = Level::Normal;
  const std::wstring target = FinalPath(path);
  for (const std::wstring& folder : trustedFolders) {
    if (Inside(target, FinalPath(folder))) {
      result.level = Level::Trusted;
      break;
    }
  }
  return result;
}

const wchar_t* Trust::Name(Level level) {
  switch (level) {
    case Level::Trusted: return L"trusted";
    case Level::Normal: return L"normal";
    default: return L"restricted";
  }
}
