#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <shlobj.h>
#include <wrl/client.h>

#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

using Microsoft::WRL::ComPtr;

namespace {

void Check(HRESULT result, const char* operation) {
  if (FAILED(result)) {
    std::cerr << operation << " failed: 0x" << std::hex << result << '\n';
    throw std::runtime_error(operation);
  }
}

UINT FindJsonCommand(HMENU menu, IContextMenu* handler) {
  for (int index = 0; index < ::GetMenuItemCount(menu); ++index) {
    const HMENU child = ::GetSubMenu(menu, index);
    if (child) {
      const UINT found = FindJsonCommand(child, handler);
      if (found) return found;
    } else {
      const UINT id = ::GetMenuItemID(menu, index);
      wchar_t verb[256]{};
      if (id && id != static_cast<UINT>(-1) &&
          SUCCEEDED(handler->GetCommandString(id - 1, GCS_HELPTEXTW, nullptr,
                                             reinterpret_cast<LPSTR>(verb), ARRAYSIZE(verb))) &&
          (std::wstring(verb) == L"Pretty Print" || std::wstring(verb) == L"整形" ||
           std::wstring(verb) == L"格式化")) {
        return id;
      }
    }
  }
  return 0;
}

void TestMenu(HMODULE module, const CLSID& clsid, const std::vector<std::wstring>& paths,
              bool invoke, bool registered) {
  const auto getClass = reinterpret_cast<HRESULT(STDAPICALLTYPE*)(REFCLSID, REFIID, void**)>(
      ::GetProcAddress(module, "DllGetClassObject"));
  if (!getClass) throw std::runtime_error("DllGetClassObject is missing");

  ComPtr<IClassFactory> factory;
  if (registered) {
    Check(::CoGetClassObject(clsid, CLSCTX_INPROC_SERVER, nullptr,
                            IID_PPV_ARGS(factory.GetAddressOf())), "CoGetClassObject");
  } else {
    Check(getClass(clsid, IID_PPV_ARGS(factory.GetAddressOf())), "DllGetClassObject");
  }
  ComPtr<IShellExtInit> initialize;
  Check(factory->CreateInstance(nullptr, IID_PPV_ARGS(initialize.GetAddressOf())), "CreateInstance");
  ComPtr<IContextMenu> handler;
  Check(initialize.As(&handler), "IContextMenu");

  std::vector<PCUITEMID_CHILD> children;
  for (const auto& path : paths) {
    PIDLIST_ABSOLUTE item = nullptr;
    const HRESULT result = ::SHParseDisplayName(path.c_str(), nullptr, &item, 0, nullptr);
    if (FAILED(result)) {
      for (const auto child : children) ::CoTaskMemFree(const_cast<ITEMIDLIST*>(child));
      Check(result, "SHParseDisplayName");
    }
    children.push_back(item);
  }
  ComPtr<IDataObject> data;
  const HRESULT result = ::SHCreateDataObject(nullptr, static_cast<UINT>(children.size()),
      children.data(), nullptr, IID_PPV_ARGS(data.GetAddressOf()));
  for (const auto child : children) ::CoTaskMemFree(const_cast<ITEMIDLIST*>(child));
  Check(result, "SHCreateDataObject");
  size_t characters = 1;
  for (const auto& path : paths) characters += path.size() + 1;
  const HGLOBAL drop = ::GlobalAlloc(GHND, sizeof(DROPFILES) + characters * sizeof(wchar_t));
  if (!drop) throw std::runtime_error("GlobalAlloc failed");
  const auto header = static_cast<DROPFILES*>(::GlobalLock(drop));
  if (!header) {
    ::GlobalFree(drop);
    throw std::runtime_error("GlobalLock failed");
  }
  header->pFiles = sizeof(DROPFILES);
  header->fWide = TRUE;
  auto destination = reinterpret_cast<wchar_t*>(header + 1);
  for (const auto& path : paths) {
    ::CopyMemory(destination, path.c_str(), (path.size() + 1) * sizeof(wchar_t));
    destination += path.size() + 1;
  }
  ::GlobalUnlock(drop);
  FORMATETC format{CF_HDROP, nullptr, DVASPECT_CONTENT, -1, TYMED_HGLOBAL};
  STGMEDIUM medium{};
  medium.tymed = TYMED_HGLOBAL;
  medium.hGlobal = drop;
  const HRESULT setData = data->SetData(&format, &medium, TRUE);
  if (FAILED(setData)) ::GlobalFree(drop);
  Check(setData, "SetData(CF_HDROP)");
  Check(initialize->Initialize(nullptr, data.Get(), nullptr), "Initialize");

  const HMENU menu = ::CreatePopupMenu();
  if (!menu) throw std::runtime_error("CreatePopupMenu failed");
  UINT command = 0;
  try {
    Check(handler->QueryContextMenu(menu, 0, 1, 0x7fff, CMF_NORMAL), "QueryContextMenu");
    command = FindJsonCommand(menu, handler.Get());
    if (!command) throw std::runtime_error("JSON formatting command is missing from the menu");
    if (invoke) {
      CMINVOKECOMMANDINFO info{};
      info.cbSize = sizeof(info);
      info.lpVerb = MAKEINTRESOURCEA(command - 1);
      info.nShow = SW_SHOWNORMAL;
      Check(handler->InvokeCommand(&info), "InvokeCommand");
    }
  } catch (...) {
    ::DestroyMenu(menu);
    throw;
  }
  ::DestroyMenu(menu);
  std::cout << "PASS COM activation, selection and JSON menu" << (invoke ? ", worker launch" : "") << '\n';
}

}

int wmain(int argc, wchar_t** argv) {
  if (argc < 4) return 2;
  ::SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOOPENFILEERRORBOX);
  const HRESULT initialized = ::CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
  if (FAILED(initialized)) return 1;
  int code = 0;
  HMODULE module = nullptr;
  try {
    CLSID clsid{};
    Check(::CLSIDFromString(argv[2], &clsid), "CLSIDFromString");
    module = ::LoadLibraryW(argv[1]);
    if (!module) {
      std::cerr << "LoadLibrary failed: " << ::GetLastError() << '\n';
      throw std::runtime_error("Native shell DLL could not be loaded");
    }
    bool invoke = false;
    bool registered = false;
    std::vector<std::wstring> paths;
    for (int index = 3; index < argc; ++index) {
      const std::wstring argument = argv[index];
      if (argument == L"--invoke") invoke = true;
      else if (argument == L"--registered") registered = true;
      else paths.push_back(argument);
    }
    if (paths.empty()) throw std::runtime_error("No test files were supplied");
    TestMenu(module, clsid, paths, invoke, registered);
    const auto canUnload = reinterpret_cast<HRESULT(STDAPICALLTYPE*)()>(
        ::GetProcAddress(module, "DllCanUnloadNow"));
    if (!canUnload || canUnload() != S_OK) throw std::runtime_error("COM references were not released");
  } catch (const std::exception& error) {
    std::cerr << error.what() << '\n';
    code = 1;
  }
  if (module) ::FreeLibrary(module);
  ::CoUninitialize();
  return code;
}
