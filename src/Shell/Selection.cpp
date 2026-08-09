#include "Selection.h"

#include "GifProbe.h"

namespace {

constexpr size_t kMaxSelection = 20000;
constexpr size_t kMaxSizeProbes = 4096;
constexpr size_t kMaxGifProbes = 64;

struct FormatMapping {
  const wchar_t* extension;
  const wchar_t* format;
};

constexpr FormatMapping kImageExtensions[] = {
    {L"png", L"PNG"},   {L"jpg", L"JPG"},   {L"jpeg", L"JPG"}, {L"webp", L"WEBP"},
    {L"heic", L"HEIC"}, {L"heif", L"HEIC"}, {L"ico", L"ICO"},  {L"gif", L"GIF"},
};

constexpr FormatMapping kVideoExtensions[] = {
    {L"mp4", L"MP4"}, {L"mov", L"MOV"},   {L"mkv", L"MKV"}, {L"m4a", L"M4A"},
    {L"avi", L"AVI"}, {L"webm", L"WEBM"}, {L"flv", L"FLV"},
};

// m4a is deliberately absent: it stays classified as video, so that selecting one
// keeps the menu it has always had. It is still offered as a conversion target.
constexpr FormatMapping kAudioExtensions[] = {
    {L"mp3", L"MP3"}, {L"wav", L"WAV"}, {L"aif", L"AIF"}, {L"aiff", L"AIFF"},
    {L"aac", L"AAC"}, {L"ogg", L"OGG"}, {L"wma", L"WMA"},
};

// Text extensions whose format is worth telling apart, because something can
// actually be converted to or from them. Everything else in kTextExtensions is
// still text, it just has no meaningful conversion.
constexpr FormatMapping kTextFormats[] = {
    {L"txt", L"TXT"},   {L"text", L"TXT"},  {L"json", L"JSON"}, {L"csv", L"CSV"},
    {L"xml", L"XML"},   {L"ini", L"INI"},   {L"cfg", L"CFG"},   {L"conf", L"CFG"},
    {L"css", L"CSS"},   {L"html", L"HTML"}, {L"htm", L"HTML"},  {L"md", L"MD"},
    {L"markdown", L"MD"},
};

constexpr const wchar_t* kTextExtensions[] = {
    L"txt",  L"text", L"md",   L"markdown", L"log",  L"csv",  L"tsv",  L"ini",
    L"cfg",  L"conf", L"json", L"xml",      L"yaml", L"yml",  L"toml", L"html",
    L"htm",  L"css",  L"js",   L"mjs",      L"cjs",  L"ts",   L"tsx",  L"jsx",
    L"c",    L"h",    L"cpp",  L"hpp",      L"cc",   L"hh",   L"cs",   L"java",
    L"kt",   L"py",   L"rb",   L"go",       L"rs",   L"php",  L"pl",   L"lua",
    L"sh",   L"bash", L"bat",  L"cmd",      L"ps1",  L"psm1", L"sql",  L"srt",
    L"vtt",  L"asc",  L"tex",  L"rst",      L"env",  L"props", L"properties",
    L"gradle", L"cmake", L"m", L"mm",       L"swift", L"vb",  L"r",    L"jl",
};

std::wstring ToLower(std::wstring value) {
  std::transform(value.begin(), value.end(), value.begin(),
                 [](wchar_t c) { return static_cast<wchar_t>(::towlower(c)); });
  return value;
}

std::wstring ExtensionOf(const std::wstring& path) {
  const size_t dot = path.find_last_of(L'.');
  if (dot == std::wstring::npos) return {};
  const size_t separator = path.find_last_of(L'\\');
  if (separator != std::wstring::npos && dot < separator) return {};
  return ToLower(path.substr(dot + 1));
}

const wchar_t* Lookup(const FormatMapping* table, size_t count, const std::wstring& extension) {
  for (size_t i = 0; i < count; ++i) {
    if (extension == table[i].extension) return table[i].format;
  }
  return nullptr;
}

bool IsTextExtension(const std::wstring& extension) {
  for (const wchar_t* candidate : kTextExtensions) {
    if (extension == candidate) return true;
  }
  return false;
}

struct Classification {
  Category category = Category::None;
  const wchar_t* format = nullptr;
};

Classification Classify(const std::wstring& path, bool isDirectory) {
  if (isDirectory) return {Category::Folder, nullptr};

  const std::wstring extension = ExtensionOf(path);
  if (extension.empty()) return {};

  if (extension == L"pdf") return {Category::Pdf, L"PDF"};
  if (extension == L"jar") return {Category::Jar, L"JAR"};

  if (const wchar_t* image =
          Lookup(kImageExtensions, std::size(kImageExtensions), extension)) {
    return {Category::Image, image};
  }
  if (const wchar_t* video =
          Lookup(kVideoExtensions, std::size(kVideoExtensions), extension)) {
    return {Category::Video, video};
  }
  if (const wchar_t* audio =
          Lookup(kAudioExtensions, std::size(kAudioExtensions), extension)) {
    return {Category::Audio, audio};
  }
  if (IsTextExtension(extension)) {
    const wchar_t* text = Lookup(kTextFormats, std::size(kTextFormats), extension);
    return {Category::Text, text ? text : L"TEXT"};
  }
  return {};
}

}

const std::vector<std::wstring>& Sel::ImageFormats() {
  static const std::vector<std::wstring> formats = {L"PNG",  L"JPG", L"WEBP",
                                                    L"HEIC", L"ICO", L"GIF"};
  return formats;
}

const std::vector<std::wstring>& Sel::VideoFormats() {
  static const std::vector<std::wstring> formats = {L"MP4", L"MOV",  L"MKV", L"M4A",
                                                    L"AVI", L"WEBM", L"FLV"};
  return formats;
}

// Conversion targets, which is why M4A appears here even though an m4a file is
// classified as video.
const std::vector<std::wstring>& Sel::AudioFormats() {
  static const std::vector<std::wstring> formats = {L"MP3", L"M4A", L"WAV", L"AIF",
                                                    L"AIFF", L"AAC", L"OGG", L"WMA"};
  return formats;
}

SelectionInfo Sel::FromDataObject(IDataObject* dataObject) {
  SelectionInfo info;
  if (!dataObject) return info;

  FORMATETC format{CF_HDROP, nullptr, DVASPECT_CONTENT, -1, TYMED_HGLOBAL};
  STGMEDIUM medium{};
  if (FAILED(dataObject->GetData(&format, &medium))) return info;

  auto* drop = static_cast<HDROP>(::GlobalLock(medium.hGlobal));
  if (!drop) {
    ::ReleaseStgMedium(&medium);
    return info;
  }

  const UINT count = ::DragQueryFileW(drop, 0xFFFFFFFFu, nullptr, 0);
  if (count == 0 || count > kMaxSelection) {
    ::GlobalUnlock(medium.hGlobal);
    ::ReleaseStgMedium(&medium);
    return info;
  }

  bool consistent = true;
  size_t sizeProbes = 0;
  size_t gifProbes = 0;
  std::vector<std::wstring> gifPaths;

  info.paths.reserve(count);
  for (UINT index = 0; index < count && consistent; ++index) {
    const UINT length = ::DragQueryFileW(drop, index, nullptr, 0);
    if (length == 0) {
      consistent = false;
      break;
    }
    std::wstring path(static_cast<size_t>(length) + 1, L'\0');
    if (::DragQueryFileW(drop, index, path.data(), length + 1) == 0) {
      consistent = false;
      break;
    }
    path.resize(length);

    WIN32_FILE_ATTRIBUTE_DATA attributes{};
    const bool haveAttributes =
        ::GetFileAttributesExW(path.c_str(), GetFileExInfoStandard, &attributes) != FALSE;
    if (!haveAttributes) {
      consistent = false;
      break;
    }
    const bool isDirectory =
        (attributes.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;

    const Classification classification = Classify(path, isDirectory);
    if (classification.category == Category::None) {
      consistent = false;
      break;
    }
    if (info.category == Category::None) {
      info.category = classification.category;
    } else if (info.category != classification.category) {
      consistent = false;
      break;
    }

    if (classification.format) info.formats.insert(classification.format);

    if (!isDirectory) {
      if (sizeProbes < kMaxSizeProbes) {
        const unsigned long long size =
            (static_cast<unsigned long long>(attributes.nFileSizeHigh) << 32) |
            attributes.nFileSizeLow;
        info.maxSize = (std::max)(info.maxSize, size);
        ++sizeProbes;
      } else {
        info.maxSize = (std::numeric_limits<unsigned long long>::max)();
      }
      if (classification.format && std::wstring(classification.format) == L"GIF" &&
          gifProbes < kMaxGifProbes) {
        gifPaths.push_back(path);
        ++gifProbes;
      }
    }

    info.paths.push_back(std::move(path));
  }

  ::GlobalUnlock(medium.hGlobal);
  ::ReleaseStgMedium(&medium);

  if (!consistent) return SelectionInfo{};

  info.allJson = info.category == Category::Text && info.formats.size() == 1 &&
                 *info.formats.begin() == L"JSON";

  for (const std::wstring& gif : gifPaths) {
    if (GifProbe::IsAnimated(gif)) {
      info.hasAnimatedGif = true;
      break;
    }
  }

  return info;
}
