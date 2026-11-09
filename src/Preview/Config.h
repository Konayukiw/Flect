#pragma once

#include "framework.h"

struct PreviewConfig {
  std::wstring fontFamily = L"HackGen Console";
  int fontSize = 13;
  std::wstring sampleText;
  std::vector<std::wstring> trustedFolders;
  std::wstring locale = L"en";
  bool darkTheme = false;
};

namespace Config {

PreviewConfig Read();

}
