#pragma once

#include "framework.h"

namespace Sniff {

struct Result {
  std::wstring name;
  std::wstring family;
  std::wstring demuxer;
};

Result Of(std::span<const uint8_t> header, std::wstring_view fileName);

}
