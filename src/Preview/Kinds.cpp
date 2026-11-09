#include "Kinds.h"

#include "Text.h"

namespace {

struct Entry {
  std::wstring_view extension;
  Kind kind;
  std::wstring_view language;
};

#define ENTRY(extension, kind, language) {L##extension, Kind::kind, L##language},
constexpr Entry kEntries[] = {
#include "Extensions.inc"
};
#undef ENTRY

struct MimeEntry {
  std::wstring_view extension;
  const wchar_t* type;
};

constexpr MimeEntry kMimes[] = {
    {L".html", L"text/html"},
    {L".htm", L"text/html"},
    {L".xhtml", L"application/xhtml+xml"},
    {L".css", L"text/css"},
    {L".js", L"text/javascript"},
    {L".mjs", L"text/javascript"},
    {L".cjs", L"text/javascript"},
    {L".json", L"application/json"},
    {L".map", L"application/json"},
    {L".wasm", L"application/wasm"},
    {L".txt", L"text/plain"},
    {L".md", L"text/plain"},
    {L".xml", L"application/xml"},
    {L".svg", L"image/svg+xml"},
    {L".png", L"image/png"},
    {L".jpg", L"image/jpeg"},
    {L".jpeg", L"image/jpeg"},
    {L".gif", L"image/gif"},
    {L".webp", L"image/webp"},
    {L".avif", L"image/avif"},
    {L".bmp", L"image/bmp"},
    {L".ico", L"image/x-icon"},
    {L".pdf", L"application/pdf"},
    {L".ttf", L"font/ttf"},
    {L".otf", L"font/otf"},
    {L".ttc", L"font/collection"},
    {L".otc", L"font/collection"},
    {L".woff", L"font/woff"},
    {L".woff2", L"font/woff2"},
    {L".mp4", L"video/mp4"},
    {L".m4v", L"video/mp4"},
    {L".webm", L"video/webm"},
    {L".mkv", L"video/x-matroska"},
    {L".mov", L"video/quicktime"},
    {L".avi", L"video/x-msvideo"},
    {L".wmv", L"video/x-ms-wmv"},
    {L".flv", L"video/x-flv"},
    {L".ogv", L"video/ogg"},
    {L".mpg", L"video/mpeg"},
    {L".mpeg", L"video/mpeg"},
    {L".3gp", L"video/3gpp"},
    {L".mp3", L"audio/mpeg"},
    {L".wav", L"audio/wav"},
    {L".flac", L"audio/flac"},
    {L".ogg", L"audio/ogg"},
    {L".oga", L"audio/ogg"},
    {L".opus", L"audio/ogg"},
    {L".m4a", L"audio/mp4"},
    {L".aac", L"audio/aac"},
    {L".wma", L"audio/x-ms-wma"},
    {L".aif", L"audio/aiff"},
    {L".aiff", L"audio/aiff"},
};

}

FileType Kinds::Lookup(std::wstring_view extension) {
  for (const Entry& entry : kEntries) {
    if (Text::SameFold(entry.extension, extension)) return {entry.kind, entry.language};
  }
  return {};
}

const wchar_t* Kinds::Name(Kind kind) {
  switch (kind) {
    case Kind::Text: return L"text";
    case Kind::Html: return L"html";
    case Kind::Markdown: return L"markdown";
    case Kind::Svg: return L"svg";
    case Kind::Pdf: return L"pdf";
    case Kind::Font: return L"font";
    case Kind::Video: return L"video";
    case Kind::Audio: return L"audio";
    default: return L"none";
  }
}

const wchar_t* Kinds::Mime(std::wstring_view extension) {
  for (const MimeEntry& entry : kMimes) {
    if (Text::SameFold(entry.extension, extension)) return entry.type;
  }
  return L"application/octet-stream";
}
