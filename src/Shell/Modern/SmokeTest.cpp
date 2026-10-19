#include "Bridge.h"
#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>

using Microsoft::WRL::ComPtr;
void Check(bool ok, const char* message) {
  if (!ok) throw std::runtime_error(message);
  std::cout << "PASS " << message << std::endl;
}

int wmain(int argc, wchar_t** argv) {
  if (argc < 2) return 2;
  bool invoke = false;
  bool checkPackaged = false;
  for (int i = 2; i < argc; ++i) {
    if (std::wstring(argv[i]) == L"--invoke") invoke = true;
    else if (std::wstring(argv[i]) == L"--packaged") checkPackaged = true;
    else return 2;
  }
  CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
  try {
    const std::filesystem::path directory(argv[1]);
    std::filesystem::create_directories(directory / L"folder");
    std::ofstream(directory / L"日本語 test.json") << "{\"z\":1,\"a\":2}";
    std::ofstream(directory / L"second.json") << "{\"b\":3}";
    std::ofstream(directory / L"unsupported.exe") << "test";
    HMODULE module = LoadLibraryW((Bridge::OwnDirectory() + L"\\FlectModernShell.dll").c_str());
    Check(module != nullptr, "load x64 native DLL");
    auto get = reinterpret_cast<HRESULT(WINAPI*)(REFCLSID, REFIID, void**)>(GetProcAddress(module, "DllGetClassObject"));
    auto unload = reinterpret_cast<HRESULT(WINAPI*)()>(GetProcAddress(module, "DllCanUnloadNow"));
    Check(get && unload, "COM exports");
    Check(unload() == S_OK, "DLL can unload before activation");
    {
      CLSID clsid{};
      CLSIDFromString(Bridge::kClsid, &clsid);
      ComPtr<IClassFactory> factory;
      Check(SUCCEEDED(get(clsid, IID_PPV_ARGS(&factory))), "create COM factory");
      Check(unload() == S_FALSE, "factory keeps DLL loaded");
      ComPtr<IExplorerCommand> root;
      Check(SUCCEEDED(factory->CreateInstance(nullptr, IID_PPV_ARGS(&root))), "create IExplorerCommand");
      ComPtr<IShellItemArray> single;
      Check(SUCCEEDED(Bridge::ShellItems({(directory / L"日本語 test.json").wstring()}, &single)), "Japanese and space-containing path");
      EXPCMDSTATE state{};
      Check(root->GetState(single.Get(), FALSE, &state) == E_PENDING, "slow classification deferred from shell UI");
      Check(SUCCEEDED(root->GetState(single.Get(), TRUE, &state)) && state == ECS_ENABLED, "supported JSON selection enabled");
      EXPCMDFLAGS flags{};
      Check(SUCCEEDED(root->GetFlags(&flags)) && flags == ECF_HASSUBCOMMANDS, "one-level modern submenu");
      ComPtr<IEnumExplorerCommand> commands;
      Check(SUCCEEDED(root->EnumSubCommands(&commands)), "enumerate operations");
      ComPtr<IExplorerCommand> format;
      ULONG fetched = 0;
      ComPtr<IExplorerCommand> item;
      unsigned int total = 0;
      while (commands->Next(1, &item, &fetched) == S_OK) {
        ++total;
        PWSTR title = nullptr;
        Check(SUCCEEDED(item->GetTitle(single.Get(), &title)) && title && *title, "operation label");
        CoTaskMemFree(title);
        Check(SUCCEEDED(item->GetFlags(&flags)) && flags == ECF_DEFAULT, "no unsupported nested modern submenu");
        GUID id{}, expected = clsid;
        unsigned long hash = 2166136261u;
        for (wchar_t c : std::wstring(L"json.pretty")) hash = (hash ^ c) * 16777619u;
        expected.Data1 ^= hash;
        item->GetCanonicalName(&id);
        if (id == expected) format = item;
        item.Reset();
      }
      Check(total >= 3 && fetched == 0, "complete enumeration");
      commands->Reset();
      commands->Skip(1);
      ComPtr<IEnumExplorerCommand> clone;
      Check(SUCCEEDED(commands->Clone(&clone)), "clone enumerator");
      ComPtr<IExplorerCommand> a, b;
      Check(commands->Next(1, &a, nullptr) == S_OK && clone->Next(1, &b, nullptr) == S_OK && a == b, "clone preserves cursor");
      ComPtr<IShellItemArray> multi;
      Bridge::ShellItems({(directory / L"日本語 test.json").wstring(), (directory / L"second.json").wstring()}, &multi);
      Check(SUCCEEDED(root->GetState(multi.Get(), TRUE, &state)) && state == ECS_ENABLED, "multiple JSON files supported");
      ComPtr<IShellItemArray> mixed;
      Bridge::ShellItems({(directory / L"日本語 test.json").wstring(), (directory / L"folder").wstring()}, &mixed);
      Check(SUCCEEDED(root->GetState(mixed.Get(), TRUE, &state)) && state == ECS_HIDDEN, "mixed file/folder selection hidden");
      ComPtr<IShellItemArray> unsupported;
      Bridge::ShellItems({(directory / L"unsupported.exe").wstring()}, &unsupported);
      Check(SUCCEEDED(root->GetState(unsupported.Get(), TRUE, &state)) && state == ECS_HIDDEN, "unsupported format hidden");
      ComPtr<IShellItemArray> folder;
      Bridge::ShellItems({(directory / L"folder").wstring()}, &folder);
      Check(SUCCEEDED(root->GetState(folder.Get(), TRUE, &state)) && state == ECS_ENABLED, "folder operations supported");
      if (invoke) Check(format && SUCCEEDED(format->Invoke(single.Get(), nullptr)), "invoke Flect through separate helper");
    }
    Check(unload() == S_OK, "all COM references released");
    FreeLibrary(module);
    if (checkPackaged) {
      CLSID clsid{};
      CLSIDFromString(Bridge::kClsid, &clsid);
      ComPtr<IExplorerCommand> packaged;
      HRESULT hr = CoCreateInstance(clsid, nullptr, CLSCTX_LOCAL_SERVER, IID_PPV_ARGS(&packaged));
      if (FAILED(hr)) std::cerr << "Packaged activation HRESULT: 0x" << std::hex << hr << std::endl;
      Check(SUCCEEDED(hr), "activate installed packaged COM surrogate");
      ComPtr<IShellItemArray> selection;
      Bridge::ShellItems({(directory / L"日本語 test.json").wstring()}, &selection);
      EXPCMDSTATE state{};
      Check(SUCCEEDED(packaged->GetState(selection.Get(), TRUE, &state)) && state == ECS_ENABLED,
            "selection marshalled to installed COM surrogate");
      ComPtr<IEnumExplorerCommand> operations;
      Check(SUCCEEDED(packaged->EnumSubCommands(&operations)), "installed submenu enumerator marshalled");
      ComPtr<IExplorerCommand> operation;
      Check(operations->Next(1, &operation, nullptr) == S_OK, "installed operation available");
      PWSTR title = nullptr;
      Check(SUCCEEDED(operation->GetTitle(selection.Get(), &title)) && title && *title,
            "installed operation title marshalled");
      CoTaskMemFree(title);
    }
  } catch (const std::exception& error) {
    std::cerr << "FAIL " << error.what() << std::endl;
    CoUninitialize();
    return 1;
  }
  CoUninitialize();
  return 0;
}
