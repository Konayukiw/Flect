#pragma once

#include "framework.h"

namespace AppLaunch {

HRESULT Run(const std::wstring& verb, const std::vector<std::wstring>& paths, HWND owner);

}