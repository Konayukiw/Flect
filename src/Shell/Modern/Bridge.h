#pragma once

#include "MenuBuilder.h"
#include <wrl/client.h>

namespace Bridge {
inline constexpr wchar_t kClsid[] = L"{7791A612-5066-4789-A781-D35675F87522}";
std::wstring AppDirectory();
std::wstring OwnDirectory();
SelectionInfo Selection(IShellItemArray* items);
HRESULT ShellItems(const std::vector<std::wstring>& paths, IShellItemArray** items);
HRESULT StartMenu(const std::vector<std::wstring>& paths, const std::wstring& menu);
HRESULT ReadSelection(const std::wstring& file, std::vector<std::wstring>& paths);
}
