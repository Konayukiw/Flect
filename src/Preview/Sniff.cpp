#include "Sniff.h"

#include "Text.h"

namespace {

using Bytes = std::span<const uint8_t>;

bool Starts(Bytes header, std::initializer_list<int> pattern, size_t offset = 0) {
  if (header.size() < offset + pattern.size()) return false;
  size_t index = offset;
  for (const int expected : pattern) {
    if (expected >= 0 && header[index] != static_cast<uint8_t>(expected)) return false;
    ++index;
  }
  return true;
}

bool Tag(Bytes header, size_t offset, std::string_view tag) {
  if (header.size() < offset + tag.size()) return false;
  return std::equal(tag.begin(), tag.end(), header.begin() + static_cast<std::ptrdiff_t>(offset),
                    [](char a, uint8_t b) { return static_cast<uint8_t>(a) == b; });
}

bool EndsWith(std::wstring_view name, std::wstring_view suffix) {
  return name.size() >= suffix.size() && Text::SameFold(name.substr(name.size() - suffix.size()), suffix);
}

std::string Ascii(Bytes header) {
  std::string text;
  text.reserve(header.size());
  for (const uint8_t value : header) text.push_back(static_cast<char>(value));
  return text;
}

std::string LowerAscii(std::string text) {
  std::transform(text.begin(), text.end(), text.begin(), [](char c) { return c >= 'A' && c <= 'Z' ? static_cast<char>(c + 32) : c; });
  return text;
}

bool IsPe(Bytes header) {
  if (header.size() < 0x40 || header[0] != 'M' || header[1] != 'Z') return false;
  const int32_t offset = static_cast<int32_t>(header[0x3C] | header[0x3D] << 8 | header[0x3E] << 16 | static_cast<uint32_t>(header[0x3F]) << 24);
  if (offset < 0 || offset > static_cast<int64_t>(header.size()) - 4) return true;
  const auto at = static_cast<size_t>(offset);
  return header[at] == 'P' && header[at + 1] == 'E' && header[at + 2] == 0 && header[at + 3] == 0;
}

bool IsLink(Bytes header) {
  if (header.size() < 20) return false;
  if (!Starts(header, {0x4C, 0x00, 0x00, 0x00})) return false;
  return Starts(header, {0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}, 4);
}

bool IsSvg(Bytes header) {
  std::string text = Ascii(header.first(std::min<size_t>(header.size(), 2048)));
  size_t start = 0;
  if (text.size() >= 3 && text.compare(0, 3, "\xEF\xBB\xBF") == 0) start = 3;
  while (start < text.size() && (text[start] == ' ' || text[start] == '\t' || text[start] == '\r' || text[start] == '\n')) ++start;
  const std::string lowered = LowerAscii(text.substr(start));
  if (lowered.rfind("<svg", 0) == 0) return true;
  return lowered.rfind("<?xml", 0) == 0 && lowered.find("<svg") != std::string::npos;
}

bool IsText(Bytes header) {
  if (header.empty()) return false;
  if (Starts(header, {0xFF, 0xFE}) || Starts(header, {0xFE, 0xFF}) || Starts(header, {0xEF, 0xBB, 0xBF})) return true;
  const size_t sample = std::min<size_t>(header.size(), 4096);
  size_t printable = 0;
  for (size_t index = 0; index < sample; ++index) {
    const uint8_t value = header[index];
    if (value == 0) return false;
    if (value == 9 || value == 10 || value == 13 || (value >= 32 && value < 127) || value >= 128) ++printable;
  }
  return printable * 10 >= sample * 9;
}

bool IsMpegTs(Bytes header, size_t packet, size_t offset) {
  if (header.size() < offset + packet * 3 + 1) return false;
  return header[offset] == 0x47 && header[offset + packet] == 0x47 && header[offset + packet * 2] == 0x47;
}

}

Sniff::Result Sniff::Of(Bytes header, std::wstring_view fileName) {
  if (header.size() >= 8 && Starts(header, {0x89, 0x50, 0x4E, 0x47})) return {L"PNG", L"image", L""};
  if (Starts(header, {0xFF, 0xD8, 0xFF})) return {L"JPEG", L"image", L""};
  if (header.size() >= 6 && Tag(header, 0, "GIF")) return {L"GIF", L"image", L""};
  if (header.size() >= 12 && Tag(header, 0, "RIFF")) {
    if (Tag(header, 8, "WEBP")) return {L"WEBP", L"image", L""};
    if (Tag(header, 8, "WAVE")) return {L"WAV", L"audio", L"wav"};
    if (Tag(header, 8, "AVI ")) return {L"AVI", L"video", L"avi"};
  }
  if (header.size() >= 12 && Tag(header, 0, "FORM") && (Tag(header, 8, "AIFF") || Tag(header, 8, "AIFC"))) return {L"AIFF", L"audio", L"aiff"};
  if (header.size() >= 12 && Tag(header, 4, "ftyp")) {
    const std::string brand = LowerAscii(Ascii(header.subspan(8, 4)));
    if (brand == "heic" || brand == "heix" || brand == "hevc" || brand == "hevx" || brand == "heim" || brand == "heis" || brand == "heif" || brand == "mif1" || brand == "msf1") return {L"HEIC", L"image", L""};
    if (brand == "avif") return {L"AVIF", L"image", L""};
    if (brand == "qt  ") return {L"MOV", L"video", L"mov"};
    if (brand == "m4a " || brand == "m4b " || brand == "m4p ") return {L"M4A", L"audio", L"mov"};
    return {L"MP4", L"video", L"mov"};
  }
  if (Starts(header, {0x1A, 0x45, 0xDF, 0xA3})) {
    if (LowerAscii(Ascii(header)).find("webm") != std::string::npos) return {L"WEBM", L"video", L"matroska"};
    return {L"MKV", L"video", L"matroska"};
  }
  if (Tag(header, 0, "FLV")) return {L"FLV", L"video", L"flv"};
  if (Starts(header, {0x00, 0x00, 0x01, 0x00})) return {L"ICO", L"image", L""};
  if (header.size() >= 5 && Tag(header, 0, "%PDF")) return {L"PDF", L"pdf", L""};
  if (header.size() >= 4 && header[0] == 0x50 && header[1] == 0x4B && (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07)) return {L"ZIP", L"archive", L""};
  if (Starts(header, {0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C})) return {L"7Z", L"archive", L""};
  if (header.size() >= 6 && Tag(header, 0, "Rar!")) return {L"RAR", L"archive", L""};
  if (Starts(header, {0x1F, 0x8B})) {
    if (EndsWith(fileName, L".tar.gz") || EndsWith(fileName, L".tgz")) return {L"TAR.GZ", L"archive", L""};
    return {L"GZIP", L"archive", L""};
  }
  if (header.size() >= 262 && Tag(header, 257, "ustar")) return {L"TAR", L"archive", L""};
  if (Tag(header, 0, "ID3")) return {L"MP3", L"audio", L"mp3"};
  if (header.size() >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0 && EndsWith(fileName, L".mp3")) return {L"MP3", L"audio", L"mp3"};
  if (header.size() >= 2 && header[0] == 0xFF && (header[1] & 0xF6) == 0xF0 && EndsWith(fileName, L".aac")) return {L"AAC", L"audio", L"aac"};
  if (Tag(header, 0, "OggS")) return {L"OGG", L"audio", L"ogg"};
  if (Tag(header, 0, "fLaC")) return {L"FLAC", L"audio", L"flac"};
  if (header.size() >= 16 && Starts(header, {0x30, 0x26, 0xB2, 0x75})) return {L"WMA", L"audio", L"asf"};
  if (IsMpegTs(header, 188, 0) || IsMpegTs(header, 192, 4)) return {L"MPEG-TS", L"video", L"mpegts"};
  if (Starts(header, {0x00, 0x00, 0x01, 0xBA})) return {L"MPEG-PS", L"video", L"mpeg"};
  if (Starts(header, {0x00, 0x01, 0x00, 0x00}) || Tag(header, 0, "true") || Tag(header, 0, "typ1")) return {L"TTF", L"font", L""};
  if (Tag(header, 0, "OTTO")) return {L"OTF", L"font", L""};
  if (Tag(header, 0, "ttcf")) return {L"TTC", L"font", L""};
  if (Tag(header, 0, "wOFF")) return {L"WOFF", L"font", L""};
  if (Tag(header, 0, "wOF2")) return {L"WOFF2", L"font", L""};
  if (IsLink(header)) return {L"LNK", L"lnk", L""};
  if (IsPe(header)) return {L"PE", L"pe", L""};
  if (IsSvg(header)) return {L"SVG", L"image", L""};
  if (IsText(header)) return {L"TEXT", L"text", L""};
  return {L"", L"binary", L""};
}
