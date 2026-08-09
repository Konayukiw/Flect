#pragma once

#include "MenuBuilder.h"
#include "Selection.h"
#include "framework.h"

class ContextMenu : public IShellExtInit, public IContextMenu {
 public:
  ContextMenu();

  IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
  IFACEMETHODIMP_(ULONG) AddRef() override;
  IFACEMETHODIMP_(ULONG) Release() override;

  IFACEMETHODIMP Initialize(PCIDLIST_ABSOLUTE folder, IDataObject* dataObject,
                            HKEY progId) override;

  IFACEMETHODIMP QueryContextMenu(HMENU menu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast,
                                  UINT flags) override;
  IFACEMETHODIMP InvokeCommand(CMINVOKECOMMANDINFO* info) override;
  IFACEMETHODIMP GetCommandString(UINT_PTR id, UINT flags, UINT* reserved, CHAR* name,
                                  UINT cchMax) override;

 private:
  ~ContextMenu();

  bool InsertNodes(HMENU menu, const std::vector<MenuNode>& nodes, UINT idCmdFirst,
                   UINT idCmdLast);

  LONG ref_ = 1;
  SelectionInfo selection_;
  std::vector<MenuNode> tree_;
  std::vector<const MenuNode*> commands_;
};
