#pragma once

#include "framework.h"

enum class Kind { None, Text, Html, Markdown, Svg, Pdf, Font, Video, Audio };

struct FileType {
  Kind kind = Kind::None;
  std::wstring_view language;
};

namespace Kinds {

FileType Lookup(std::wstring_view extension);
const wchar_t* Name(Kind kind);
const wchar_t* Mime(std::wstring_view extension);

}
