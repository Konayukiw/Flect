#pragma once

#include "framework.h"
#include "Labels.g.h"

enum class Lang { En, Ja, Zh };

namespace Labels {

inline Lang DetectSystem() {
  switch (PRIMARYLANGID(::GetUserDefaultUILanguage())) {
    case LANG_JAPANESE:
      return Lang::Ja;
    case LANG_CHINESE:
      return Lang::Zh;
    default:
      return Lang::En;
  }
}

inline Lang Resolve(const std::wstring& tag) {
  if (tag == L"en") return Lang::En;
  if (tag == L"ja") return Lang::Ja;
  if (tag.rfind(L"zh", 0) == 0) return Lang::Zh;
  return DetectSystem();
}

inline const LabelTable& For(Lang language) {
  switch (language) {
    case Lang::Ja:
      return kJa;
    case Lang::Zh:
      return kZh;
    case Lang::En:
    default:
      return kEn;
  }
}

}
