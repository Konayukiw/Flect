#include "Text.h"

#include <bcrypt.h>

namespace {

int HexValue(wchar_t c) {
  if (c >= L'0' && c <= L'9') return c - L'0';
  if (c >= L'a' && c <= L'f') return c - L'a' + 10;
  if (c >= L'A' && c <= L'F') return c - L'A' + 10;
  return -1;
}

}

std::string Text::ToUtf8(std::wstring_view input) {
  if (input.empty()) return {};
  const int size = ::WideCharToMultiByte(CP_UTF8, 0, input.data(), static_cast<int>(input.size()), nullptr, 0, nullptr, nullptr);
  std::string output(static_cast<size_t>(size), '\0');
  ::WideCharToMultiByte(CP_UTF8, 0, input.data(), static_cast<int>(input.size()), output.data(), size, nullptr, nullptr);
  return output;
}

std::wstring Text::FromUtf8(std::string_view input) {
  if (input.empty()) return {};
  const int size = ::MultiByteToWideChar(CP_UTF8, 0, input.data(), static_cast<int>(input.size()), nullptr, 0);
  std::wstring output(static_cast<size_t>(size), L'\0');
  ::MultiByteToWideChar(CP_UTF8, 0, input.data(), static_cast<int>(input.size()), output.data(), size);
  return output;
}

std::wstring Text::Lower(std::wstring_view input) {
  std::wstring output(input);
  std::transform(output.begin(), output.end(), output.begin(), [](wchar_t c) { return static_cast<wchar_t>(std::towlower(c)); });
  return output;
}

std::wstring Text::Json(std::wstring_view input) {
  std::wstring output = L"\"";
  for (const wchar_t c : input) {
    switch (c) {
      case L'"': output += L"\\\""; break;
      case L'\\': output += L"\\\\"; break;
      case L'\b': output += L"\\b"; break;
      case L'\f': output += L"\\f"; break;
      case L'\n': output += L"\\n"; break;
      case L'\r': output += L"\\r"; break;
      case L'\t': output += L"\\t"; break;
      default:
        if (c < 0x20 || c == 0x2028 || c == 0x2029) {
          wchar_t escaped[8];
          ::StringCchPrintfW(escaped, ARRAYSIZE(escaped), L"\\u%04x", static_cast<unsigned>(c));
          output += escaped;
        } else {
          output += c;
        }
    }
  }
  output += L'"';
  return output;
}

std::wstring Text::EncodePath(std::wstring_view input) {
  static constexpr wchar_t kDigits[] = L"0123456789ABCDEF";
  std::wstring output;
  for (const char raw : ToUtf8(input)) {
    const auto c = static_cast<unsigned char>(raw);
    const bool plain = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' ||
                       c == '_' || c == '.' || c == '~' || c == '/';
    if (plain) {
      output += static_cast<wchar_t>(c);
    } else {
      output += L'%';
      output += kDigits[c >> 4];
      output += kDigits[c & 15];
    }
  }
  return output;
}

std::wstring Text::DecodePath(std::wstring_view input) {
  std::string bytes;
  for (size_t i = 0; i < input.size(); ++i) {
    if (input[i] == L'%' && i + 2 < input.size()) {
      const int high = HexValue(input[i + 1]);
      const int low = HexValue(input[i + 2]);
      if (high >= 0 && low >= 0) {
        bytes += static_cast<char>(high << 4 | low);
        i += 2;
        continue;
      }
    }
    bytes += ToUtf8(input.substr(i, 1));
  }
  return FromUtf8(bytes);
}

std::wstring Text::RandomHex(size_t bytes) {
  static constexpr wchar_t kDigits[] = L"0123456789abcdef";
  std::vector<BYTE> buffer(bytes);
  ::BCryptGenRandom(nullptr, buffer.data(), static_cast<ULONG>(buffer.size()), BCRYPT_USE_SYSTEM_PREFERRED_RNG);
  std::wstring output;
  for (const BYTE b : buffer) {
    output += kDigits[b >> 4];
    output += kDigits[b & 15];
  }
  return output;
}

std::wstring Text::Take(LPWSTR raw) {
  std::wstring output = raw ? raw : L"";
  if (raw) ::CoTaskMemFree(raw);
  return output;
}

bool Text::SameFold(std::wstring_view left, std::wstring_view right) {
  return ::CompareStringOrdinal(left.data(), static_cast<int>(left.size()), right.data(), static_cast<int>(right.size()), TRUE) == CSTR_EQUAL;
}
