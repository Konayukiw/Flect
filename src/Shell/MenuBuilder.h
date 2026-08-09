#pragma once

#include "Config.h"
#include "Selection.h"
#include "framework.h"

struct MenuNode {
  std::wstring id;
  std::wstring label;
  std::wstring verb;
  std::vector<MenuNode> children;
};

namespace MenuBuilder {

std::vector<MenuNode> Build(const SelectionInfo& selection, const ShellConfig& config);

}
