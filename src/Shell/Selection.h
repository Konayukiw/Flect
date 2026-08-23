#pragma once

#include "framework.h"

enum class Category { None, Folder, Pdf, Image, Video, Audio, Text, Jar, Archive };

struct SelectionInfo {
  std::vector<std::wstring> paths;
  Category category = Category::None;

  std::set<std::wstring> formats;

  unsigned long long maxSize = 0;

  bool allJson = false;

  bool allPython = false;

  bool hasAnimatedGif = false;

  bool Empty() const noexcept { return paths.empty(); }
};

namespace Sel {

SelectionInfo FromDataObject(IDataObject* dataObject);

const std::vector<std::wstring>& ImageFormats();
const std::vector<std::wstring>& VideoFormats();
const std::vector<std::wstring>& AudioFormats();

}
