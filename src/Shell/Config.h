#pragma once

#include <utility>

#include "framework.h"

struct ShellConfig {
  std::wstring language;
  std::set<std::wstring> hidden;
  std::vector<std::pair<std::wstring, std::wstring>> presets;
};

namespace Config {

ShellConfig Get();

}
