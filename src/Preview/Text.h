#pragma once

#include "framework.h"

namespace Text {

std::string ToUtf8(std::wstring_view input);
std::wstring FromUtf8(std::string_view input);
std::wstring Lower(std::wstring_view input);
std::wstring Json(std::wstring_view input);
std::wstring EncodePath(std::wstring_view input);
std::wstring DecodePath(std::wstring_view input);
std::wstring RandomHex(size_t bytes);
std::wstring Take(LPWSTR raw);
bool SameFold(std::wstring_view left, std::wstring_view right);

}
