#include "ContextMenu.h"

#include "Launcher.h"
#include "Module.h"
#include "Resource.h"

namespace {

HBITMAP LoadMenuIcon() {
  const int cx = ::GetSystemMetrics(SM_CXSMICON);
  const int cy = ::GetSystemMetrics(SM_CYSMICON);

  auto icon = static_cast<HICON>(::LoadImageW(Module::Instance(), MAKEINTRESOURCEW(IDI_FLECT),
                                              IMAGE_ICON, cx, cy, LR_DEFAULTCOLOR));
  if (!icon) return nullptr;

  BITMAPINFO info{};
  info.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
  info.bmiHeader.biWidth = cx;
  info.bmiHeader.biHeight = -cy;
  info.bmiHeader.biPlanes = 1;
  info.bmiHeader.biBitCount = 32;
  info.bmiHeader.biCompression = BI_RGB;

  void* bits = nullptr;
  HDC screen = ::GetDC(nullptr);
  HBITMAP bitmap = ::CreateDIBSection(screen, &info, DIB_RGB_COLORS, &bits, nullptr, 0);
  ::ReleaseDC(nullptr, screen);

  if (bitmap) {
    HDC dc = ::CreateCompatibleDC(nullptr);
    HGDIOBJ previous = ::SelectObject(dc, bitmap);
    ::DrawIconEx(dc, 0, 0, icon, cx, cy, 0, nullptr, DI_NORMAL);
    ::SelectObject(dc, previous);
    ::DeleteDC(dc);
  }

  ::DestroyIcon(icon);
  return bitmap;
}

}

ContextMenu::ContextMenu() { Module::AddRef(); }

ContextMenu::~ContextMenu() {
  if (icon_) ::DeleteObject(icon_);
  Module::Release();
}

IFACEMETHODIMP ContextMenu::QueryInterface(REFIID riid, void** ppv) {
  if (!ppv) return E_POINTER;
  if (::IsEqualIID(riid, IID_IUnknown) || ::IsEqualIID(riid, IID_IShellExtInit)) {
    *ppv = static_cast<IShellExtInit*>(this);
  } else if (::IsEqualIID(riid, IID_IContextMenu)) {
    *ppv = static_cast<IContextMenu*>(this);
  } else {
    *ppv = nullptr;
    return E_NOINTERFACE;
  }
  AddRef();
  return S_OK;
}

IFACEMETHODIMP_(ULONG) ContextMenu::AddRef() {
  return static_cast<ULONG>(::InterlockedIncrement(&ref_));
}

IFACEMETHODIMP_(ULONG) ContextMenu::Release() {
  const LONG remaining = ::InterlockedDecrement(&ref_);
  if (remaining == 0) delete this;
  return static_cast<ULONG>(remaining);
}

IFACEMETHODIMP ContextMenu::Initialize(PCIDLIST_ABSOLUTE, IDataObject* dataObject, HKEY) {
  selection_ = Sel::FromDataObject(dataObject);
  return selection_.Empty() ? E_INVALIDARG : S_OK;
}

bool ContextMenu::InsertNodes(HMENU menu, const std::vector<MenuNode>& nodes, UINT idCmdFirst,
                              UINT idCmdLast) {
  UINT position = 0;
  for (const MenuNode& node : nodes) {
    const UINT id = idCmdFirst + static_cast<UINT>(commands_.size());
    if (id > idCmdLast) return false;

    MENUITEMINFOW item{};
    item.cbSize = sizeof(item);
    item.fMask = MIIM_STRING | MIIM_ID;
    item.wID = id;
    item.dwTypeData = const_cast<LPWSTR>(node.label.c_str());

    commands_.push_back(&node);

    HMENU child = nullptr;
    if (!node.children.empty()) {
      child = ::CreatePopupMenu();
      if (!child) return false;
      if (!InsertNodes(child, node.children, idCmdFirst, idCmdLast)) {
        ::DestroyMenu(child);
        return false;
      }
      item.fMask |= MIIM_SUBMENU;
      item.hSubMenu = child;
    }

    if (!::InsertMenuItemW(menu, position++, TRUE, &item)) {
      if (child) ::DestroyMenu(child);
      return false;
    }
  }
  return true;
}

IFACEMETHODIMP ContextMenu::QueryContextMenu(HMENU menu, UINT indexMenu, UINT idCmdFirst,
                                             UINT idCmdLast, UINT flags) {
  if (flags & CMF_DEFAULTONLY) return MAKE_HRESULT(SEVERITY_SUCCESS, 0, 0);

  tree_ = MenuBuilder::Build(selection_);
  commands_.clear();
  if (tree_.empty()) return MAKE_HRESULT(SEVERITY_SUCCESS, 0, 0);

  HMENU submenu = ::CreatePopupMenu();
  if (!submenu) return MAKE_HRESULT(SEVERITY_SUCCESS, 0, 0);

  commands_.push_back(nullptr);
  if (!InsertNodes(submenu, tree_, idCmdFirst, idCmdLast)) {
    ::DestroyMenu(submenu);
    commands_.clear();
    return MAKE_HRESULT(SEVERITY_SUCCESS, 0, 0);
  }

  if (!icon_) icon_ = LoadMenuIcon();

  MENUITEMINFOW root{};
  root.cbSize = sizeof(root);
  root.fMask = MIIM_STRING | MIIM_SUBMENU | MIIM_ID;
  root.wID = idCmdFirst;
  root.hSubMenu = submenu;
  root.dwTypeData = const_cast<LPWSTR>(BRAND_NAME);

  if (icon_) {
    root.fMask |= MIIM_BITMAP;
    root.hbmpItem = icon_;
  }

  if (!::InsertMenuItemW(menu, indexMenu, TRUE, &root)) {
    ::DestroyMenu(submenu);
    commands_.clear();
    return MAKE_HRESULT(SEVERITY_SUCCESS, 0, 0);
  }

  return MAKE_HRESULT(SEVERITY_SUCCESS, 0, static_cast<USHORT>(commands_.size()));
}

IFACEMETHODIMP ContextMenu::InvokeCommand(CMINVOKECOMMANDINFO* info) {
  if (!info) return E_INVALIDARG;

  if (!IS_INTRESOURCE(info->lpVerb)) return E_INVALIDARG;

  const size_t offset = static_cast<size_t>(LOWORD(info->lpVerb));
  if (offset >= commands_.size()) return E_INVALIDARG;

  const MenuNode* node = commands_[offset];
  if (!node || node->verb.empty()) return E_INVALIDARG;

  return Launcher::Run(node->verb, selection_.paths, info->hwnd);
}

IFACEMETHODIMP ContextMenu::GetCommandString(UINT_PTR id, UINT flags, UINT*, CHAR* name, UINT cchMax) {
  if (id >= commands_.size() || !name || cchMax == 0) return E_INVALIDARG;
  const MenuNode* node = commands_[id];
  if (!node) return E_INVALIDARG;

  switch (flags) {
    case GCS_HELPTEXTW:
      return ::StringCchCopyW(reinterpret_cast<LPWSTR>(name), cchMax, node->label.c_str());
    case GCS_HELPTEXTA:
      ::WideCharToMultiByte(CP_ACP, 0, node->label.c_str(), -1, name, static_cast<int>(cchMax),
                            nullptr, nullptr);
      return S_OK;
    default:
      return E_NOTIMPL;
  }
}
