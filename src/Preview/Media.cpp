#include "Media.h"

#include "Context.h"
#include "Module.h"
#include "Text.h"

namespace {

constexpr DWORD kTimeoutMs = 30 * 60 * 1000;
constexpr SIZE_T kMemoryLimit = 4ull * 1024 * 1024 * 1024;
constexpr ULONGLONG kStaleAfter = 24ull * 60 * 60 * 10000000;

std::wstring TempFolder() {
  std::wstring folder = Context::DataFolder();
  if (folder.empty()) return {};
  folder += L"\\Temp";
  ::CreateDirectoryW(folder.c_str(), nullptr);
  return folder;
}

void SweepStale(const std::wstring& folder) {
  FILETIME now{};
  ::GetSystemTimeAsFileTime(&now);
  const ULONGLONG current = static_cast<ULONGLONG>(now.dwHighDateTime) << 32 | now.dwLowDateTime;

  WIN32_FIND_DATAW entry{};
  HANDLE find = ::FindFirstFileW((folder + L"\\*").c_str(), &entry);
  if (find == INVALID_HANDLE_VALUE) return;
  do {
    if (entry.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) continue;
    const ULONGLONG written = static_cast<ULONGLONG>(entry.ftLastWriteTime.dwHighDateTime) << 32 | entry.ftLastWriteTime.dwLowDateTime;
    if (current > written && current - written > kStaleAfter) ::DeleteFileW((folder + L"\\" + entry.cFileName).c_str());
  } while (::FindNextFileW(find, &entry));
  ::FindClose(find);
}

HANDLE CreateLimitedJob() {
  HANDLE job = ::CreateJobObjectW(nullptr, nullptr);
  if (!job) return nullptr;

  JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits{};
  limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_ACTIVE_PROCESS | JOB_OBJECT_LIMIT_PROCESS_MEMORY | JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION;
  limits.BasicLimitInformation.ActiveProcessLimit = 1;
  limits.ProcessMemoryLimit = kMemoryLimit;

  JOBOBJECT_BASIC_UI_RESTRICTIONS interfaceLimits{};
  interfaceLimits.UIRestrictionsClass = JOB_OBJECT_UILIMIT_DESKTOP | JOB_OBJECT_UILIMIT_DISPLAYSETTINGS | JOB_OBJECT_UILIMIT_EXITWINDOWS | JOB_OBJECT_UILIMIT_GLOBALATOMS | JOB_OBJECT_UILIMIT_HANDLES | JOB_OBJECT_UILIMIT_READCLIPBOARD | JOB_OBJECT_UILIMIT_SYSTEMPARAMETERS | JOB_OBJECT_UILIMIT_WRITECLIPBOARD;

  if (!::SetInformationJobObject(job, JobObjectExtendedLimitInformation, &limits, sizeof(limits)) ||
      !::SetInformationJobObject(job, JobObjectBasicUIRestrictions, &interfaceLimits, sizeof(interfaceLimits))) {
    ::CloseHandle(job);
    return nullptr;
  }
  return job;
}

HANDLE CreateLimitedToken() {
  HANDLE current = nullptr;
  if (!::OpenProcessToken(::GetCurrentProcess(), TOKEN_DUPLICATE | TOKEN_ASSIGN_PRIMARY | TOKEN_QUERY, &current)) return nullptr;
  HANDLE restricted = nullptr;
  const BOOL ok = ::CreateRestrictedToken(current, DISABLE_MAX_PRIVILEGE, 0, nullptr, 0, nullptr, 0, nullptr, &restricted);
  ::CloseHandle(current);
  return ok ? restricted : nullptr;
}

}

std::unique_ptr<Conversion> Conversion::Start(HWND window, UINT message, std::wstring input, std::wstring demuxer, bool audioOnly) {
  const std::wstring folder = TempFolder();
  if (folder.empty()) return nullptr;
  SweepStale(folder);

  std::wstring executable = Module::Directory() + L"\\deps\\ffmpeg.exe";
  if (::GetFileAttributesW(executable.c_str()) == INVALID_FILE_ATTRIBUTES) return nullptr;

  std::unique_ptr<Conversion> conversion(new (std::nothrow) Conversion());
  if (!conversion) return nullptr;
  conversion->window_ = window;
  conversion->message_ = message;
  conversion->executable_ = std::move(executable);
  conversion->input_ = std::move(input);
  conversion->demuxer_ = std::move(demuxer);
  conversion->audioOnly_ = audioOnly;
  conversion->output_ = folder + L"\\" + Text::RandomHex(12) + (audioOnly ? L".m4a" : L".mp4");
  conversion->cancel_ = ::CreateEventW(nullptr, TRUE, FALSE, nullptr);
  if (!conversion->cancel_) return nullptr;
  conversion->worker_ = std::thread([raw = conversion.get()] { raw->Run(); });
  return conversion;
}

Conversion::~Conversion() {
  if (cancel_) ::SetEvent(cancel_);
  if (worker_.joinable()) worker_.join();
  if (cancel_) ::CloseHandle(cancel_);
  if (!output_.empty()) ::DeleteFileW(output_.c_str());
}

void Conversion::Run() {
  std::wstring command = L"\"" + executable_ + L"\" -nostdin -hide_banner -loglevel error -protocol_whitelist file -f " + demuxer_ + L" -i \"file:" + input_ + L"\"";
  if (audioOnly_) {
    command += L" -vn -map 0:a:0 -c:a aac -b:a 192k -movflags +faststart";
  } else {
    command += L" -map 0:v:0 -map 0:a:0? -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p -c:a aac -b:a 160k -sn -dn -movflags +faststart";
  }
  command += L" \"" + output_ + L"\"";

  bool success = false;
  HANDLE job = CreateLimitedJob();
  HANDLE token = CreateLimitedToken();
  if (job && token) {
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    std::vector<wchar_t> line(command.begin(), command.end());
    line.push_back(L'\0');
    if (::CreateProcessAsUserW(token, executable_.c_str(), line.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW | CREATE_SUSPENDED, nullptr, nullptr, &startup, &process)) {
      if (::AssignProcessToJobObject(job, process.hProcess)) {
        ::ResumeThread(process.hThread);
        const HANDLE waits[] = {process.hProcess, cancel_};
        const DWORD result = ::WaitForMultipleObjects(2, waits, FALSE, kTimeoutMs);
        DWORD code = 1;
        if (result == WAIT_OBJECT_0 && ::GetExitCodeProcess(process.hProcess, &code)) success = code == 0;
      }
      ::TerminateJobObject(job, 1);
      ::CloseHandle(process.hThread);
      ::CloseHandle(process.hProcess);
    }
  }
  if (token) ::CloseHandle(token);
  if (job) ::CloseHandle(job);
  if (::WaitForSingleObject(cancel_, 0) != WAIT_OBJECT_0) ::PostMessageW(window_, message_, success ? 1 : 0, 0);
}
