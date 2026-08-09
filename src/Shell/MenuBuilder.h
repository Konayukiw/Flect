#pragma once

#include "Selection.h"
#include "framework.h"

struct MenuNode {
  std::wstring label;
  std::wstring verb;
  std::vector<MenuNode> children;
};

namespace MenuBuilder {

std::vector<MenuNode> Build(const SelectionInfo& selection);

}