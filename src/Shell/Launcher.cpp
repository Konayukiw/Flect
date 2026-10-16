#include "Launcher.h"

#include "Module.h"

namespace {

std::wstring ScratchDirectory() {
  wchar_t temp[MAX_PATH + 1]{};
  const DWORD length = ::GetTempPathW(MAX_PATH, temp);
  if (length == 0 || length > MAX_PATH) return {};
  std::wstring directory(temp, length);
  directory += BRAND_NAME;
  ::CreateDirectoryW(directory.c_str(), nullptr);
  return directory;
}

std::wstring NewSelectionFilePath() {
  const std::wstring directory = ScratchDirectory();
  if (directory.empty()) return {};

  GUID guid{};
  if (FAILED(::CoCreateGuid(&guid))) return {};
  wchar_t token[40]{};
  if (::StringFromGUID2(guid, token, ARRAYSIZE(token)) == 0) return {};

  return directory + L"\\sel-" + token + L".txt";
}

bool WriteSelectionFile(const std::wstring& path, const std::vector<std::wstring>& items) {
  std::string utf8;
  utf8 += "\xEF\xBB\xBF";
  for (const std::wstring& item : items) {
    const int required =
        ::WideCharToMultiByte(CP_UTF8, 0, item.c_str(), static_cast<int>(item.size()),
                              nullptr, 0, nullptr, nullptr);
    if (required <= 0) return false;
    const size_t offset = utf8.size();
    utf8.resize(offset + static_cast<size_t>(required));
    ::WideCharToMultiByte(CP_UTF8, 0, item.c_str(), static_cast<int>(item.size()),
                          utf8.data() + offset, required, nullptr, nullptr);
    utf8 += "\r\n";
  }

  const HANDLE file = ::CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS,
                                    FILE_ATTRIBUTE_NORMAL, nullptr);
  if (file == INVALID_HANDLE_VALUE) return false;

  DWORD written = 0;
  const BOOL ok = ::WriteFile(file, utf8.data(), static_cast<DWORD>(utf8.size()), &written,
                              nullptr);
  ::CloseHandle(file);
  return ok && written == utf8.size();
}

void ReportFailure(HWND owner, const std::wstring& text) {
  ::MessageBoxW(owner, text.c_str(), BRAND_NAME, MB_OK | MB_ICONERROR);
}

}

HRESULT Launcher::Run(const std::wstring& verb, const std::vector<std::wstring>& paths,
                      HWND owner) {
  const std::wstring installDirectory = Module::Directory();
  if (installDirectory.empty()) return E_FAIL;

  const std::wstring executable = installDirectory + L"\\" + BRAND_APP_EXE;
  if (::GetFileAttributesW(executable.c_str()) == INVALID_FILE_ATTRIBUTES) {
    ReportFailure(owner, std::wstring(L"Worker executable not found:\n") + executable);
    return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
  }

  const std::wstring selectionFile = NewSelectionFilePath();
  if (selectionFile.empty() || !WriteSelectionFile(selectionFile, paths)) {
    ReportFailure(owner, L"Could not stage the selection for processing.");
    return E_FAIL;
  }

  std::wstring commandLine = L"\"" + executable + L"\" --task \"" + verb + L"\" --input \"" +
                             selectionFile + L"\"";

  STARTUPINFOW startup{};
  startup.cb = sizeof(startup);
  PROCESS_INFORMATION process{};

  const BOOL started =
      ::CreateProcessW(executable.c_str(), commandLine.data(), nullptr, nullptr, FALSE,
                       CREATE_UNICODE_ENVIRONMENT, nullptr, installDirectory.c_str(),
                       &startup, &process);
  if (!started) {
    const DWORD error = ::GetLastError();
    ::DeleteFileW(selectionFile.c_str());
    ReportFailure(owner, L"Could not start the worker process.");
    return HRESULT_FROM_WIN32(error);
  }

  ::CloseHandle(process.hThread);
  ::CloseHandle(process.hProcess);
  return S_OK;
}
