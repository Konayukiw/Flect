#include "GifProbe.h"

namespace {

constexpr unsigned long long kScanBudget = 4ull * 1024 * 1024;

constexpr BYTE kExtensionIntroducer = 0x21;
constexpr BYTE kImageDescriptor = 0x2C;
constexpr BYTE kTrailer = 0x3B;
constexpr BYTE kApplicationExtension = 0xFF;

class Reader {
 public:
  explicit Reader(HANDLE file) : file_(file) {}

  bool ReadByte(BYTE& value) {
    if (cursor_ >= filled_ && !Fill()) return false;
    value = buffer_[cursor_++];
    ++consumed_;
    return true;
  }

  bool ReadBytes(BYTE* destination, size_t count) {
    for (size_t i = 0; i < count; ++i) {
      if (!ReadByte(destination[i])) return false;
    }
    return true;
  }

  bool Skip(unsigned long long count) {
    const unsigned long long buffered = filled_ - cursor_;
    if (count <= buffered) {
      cursor_ += static_cast<size_t>(count);
      consumed_ += count;
      return true;
    }
    const unsigned long long remainder = count - buffered;
    cursor_ = filled_ = 0;
    LARGE_INTEGER distance{};
    distance.QuadPart = static_cast<LONGLONG>(remainder);
    if (!::SetFilePointerEx(file_, distance, nullptr, FILE_CURRENT)) return false;
    consumed_ += count;
    return true;
  }

  unsigned long long Consumed() const { return consumed_; }

 private:
  bool Fill() {
    DWORD read = 0;
    if (!::ReadFile(file_, buffer_, static_cast<DWORD>(sizeof(buffer_)), &read, nullptr)) {
      return false;
    }
    cursor_ = 0;
    filled_ = read;
    return read > 0;
  }

  HANDLE file_;
  BYTE buffer_[16 * 1024]{};
  size_t cursor_ = 0;
  size_t filled_ = 0;
  unsigned long long consumed_ = 0;
};

unsigned long long ColorTableBytes(BYTE packed) {
  if ((packed & 0x80) == 0) return 0;
  return 3ull * (1ull << ((packed & 0x07) + 1));
}

bool SkipSubBlocks(Reader& reader) {
  for (;;) {
    BYTE length = 0;
    if (!reader.ReadByte(length)) return false;
    if (length == 0) return true;
    if (!reader.Skip(length)) return false;
    if (reader.Consumed() > kScanBudget) return false;
  }
}

}

bool GifProbe::IsAnimated(const std::wstring& path) {
  const HANDLE file =
      ::CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr,
                    OPEN_EXISTING, FILE_FLAG_SEQUENTIAL_SCAN, nullptr);
  if (file == INVALID_HANDLE_VALUE) return false;

  bool animated = false;
  Reader reader(file);

  for (;;) {
    BYTE header[13]{};
    if (!reader.ReadBytes(header, sizeof(header))) break;
    if (::memcmp(header, "GIF", 3) != 0) break;

    if (!reader.Skip(ColorTableBytes(header[10]))) break;

    int frames = 0;
    bool parsing = true;
    while (parsing && reader.Consumed() <= kScanBudget) {
      BYTE block = 0;
      if (!reader.ReadByte(block)) break;

      if (block == kTrailer) break;

      if (block == kExtensionIntroducer) {
        BYTE label = 0;
        if (!reader.ReadByte(label)) break;
        if (label == kApplicationExtension) {
          BYTE size = 0;
          if (!reader.ReadByte(size)) break;
          std::vector<BYTE> identifier(size);
          if (size > 0 && !reader.ReadBytes(identifier.data(), size)) break;
          if (size >= 8 && ::memcmp(identifier.data(), "NETSCAPE", 8) == 0) {
            animated = true;
            parsing = false;
            break;
          }
          if (!SkipSubBlocks(reader)) break;
        } else if (!SkipSubBlocks(reader)) {
          break;
        }
        continue;
      }

      if (block == kImageDescriptor) {
        if (++frames >= 2) {
          animated = true;
          break;
        }
        BYTE descriptor[9]{};
        if (!reader.ReadBytes(descriptor, sizeof(descriptor))) break;
        if (!reader.Skip(ColorTableBytes(descriptor[8]))) break;
        BYTE lzwMinimumCodeSize = 0;
        if (!reader.ReadByte(lzwMinimumCodeSize)) break;
        if (!SkipSubBlocks(reader)) break;
        continue;
      }
      break;
    }
    break;
  }

  ::CloseHandle(file);
  return animated;
}
