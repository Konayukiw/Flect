#pragma once

#include "framework.h"

namespace Trust {

enum class Level { Restricted, Normal, Trusted };

struct Result {
  Level level = Level::Restricted;
  int zone = -1;
  bool marked = false;
};

Result Evaluate(const std::wstring& path, const std::vector<std::wstring>& trustedFolders);
const wchar_t* Name(Level level);
std::wstring FinalPath(const std::wstring& path);

}
