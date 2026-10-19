#include "Bridge.h"
#include "Launcher.h"

namespace {
bool InsertNodes(HMENU menu, const std::vector<MenuNode>& nodes,
                 std::vector<const MenuNode*>& commands) {
  for (const auto& node : nodes) {
    MENUITEMINFOW item{sizeof(item)};
    item.fMask = MIIM_STRING | MIIM_ID;
    item.wID = static_cast<UINT>(commands.size() + 1);
    item.dwTypeData = const_cast<LPWSTR>(node.label.c_str());
    commands.push_back(&node);
    HMENU child = nullptr;
    if (!node.children.empty()) {
      child = CreatePopupMenu();
      if (!child || !InsertNodes(child, node.children, commands)) {
        if (child) DestroyMenu(child);
        return false;
      }
      item.fMask |= MIIM_SUBMENU;
      item.hSubMenu = child;
    }
    if (!InsertMenuItemW(menu, GetMenuItemCount(menu), TRUE, &item)) {
      if (child) DestroyMenu(child);
      return false;
    }
  }
  return true;
}

HRESULT Run() {
  int count = 0;
  auto args = CommandLineToArgvW(GetCommandLineW(), &count);
  if (!args) return E_OUTOFMEMORY;
  std::wstring file, menuId;
  for (int i = 1; i + 1 < count; ++i) {
    if (std::wstring(args[i]) == L"--input") file = args[++i];
    else if (std::wstring(args[i]) == L"--menu") menuId = args[++i];
  }
  LocalFree(args);
  if (file.empty()) {
    auto result = ShellExecuteW(nullptr, L"open",
        (Bridge::AppDirectory() + L"\\" + BRAND_APP_EXE).c_str(), nullptr, nullptr, SW_SHOWNORMAL);
    return reinterpret_cast<INT_PTR>(result) > 32 ? S_OK : E_FAIL;
  }
  std::vector<std::wstring> paths;
  HRESULT hr = Bridge::ReadSelection(file, paths);
  if (FAILED(hr)) return hr;
  Microsoft::WRL::ComPtr<IShellItemArray> items;
  hr = Bridge::ShellItems(paths, &items);
  if (FAILED(hr)) return hr;
  const auto selection = Bridge::Selection(items.Get());
  const auto tree = MenuBuilder::Build(selection, Config::Get());
  const MenuNode* selected = nullptr;
  for (const auto& node : tree) if (node.id == menuId) { selected = &node; break; }
  if (!selected) return E_INVALIDARG;
  if (selected->children.empty()) return Launcher::Run(selected->verb, selection.paths, nullptr);

  HMENU menu = CreatePopupMenu();
  if (!menu) return HRESULT_FROM_WIN32(GetLastError());
  std::vector<const MenuNode*> commands;
  if (!InsertNodes(menu, selected->children, commands)) { DestroyMenu(menu); return E_FAIL; }
  POINT point{};
  GetCursorPos(&point);
  HWND owner = CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_TOPMOST, L"STATIC", L"Flect",
      WS_POPUP, point.x, point.y, 1, 1, nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
  if (!owner) { DestroyMenu(menu); return E_FAIL; }
  ShowWindow(owner, SW_SHOWNORMAL);
  SetForegroundWindow(owner);
  const UINT choice = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON,
                                       point.x, point.y, owner, nullptr);
  PostMessageW(owner, WM_NULL, 0, 0);
  DestroyWindow(owner);
  DestroyMenu(menu);
  if (choice && choice <= commands.size())
    return Launcher::Run(commands[choice - 1]->verb, selection.paths, nullptr);
  return S_OK;
}
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
  SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
  HRESULT init = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
  if (FAILED(init)) return 1;
  HRESULT result = E_FAIL;
  try { result = Run(); } catch (...) { result = E_FAIL; }
  if (FAILED(result)) {
    wchar_t message[256]{};
    StringCchPrintfW(message, ARRAYSIZE(message),
        L"Flect menu could not run (0x%08X). Check that Flect is still installed.",
        static_cast<unsigned int>(result));
    MessageBoxW(nullptr, message, L"Flect", MB_OK | MB_ICONERROR);
  }
  CoUninitialize();
  return FAILED(result) ? 1 : 0;
}
