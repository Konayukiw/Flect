#include "MenuBuilder.h"

#include <cstdlib>

#include "Labels.h"

namespace {

constexpr long long kMiB = 1024ll * 1024ll;

MenuNode Leaf(std::wstring id, std::wstring label, std::wstring verb) {
  MenuNode node;
  node.id = std::move(id);
  node.label = std::move(label);
  node.verb = std::move(verb);
  return node;
}

MenuNode Popup(std::wstring id, std::wstring label, std::vector<MenuNode> children) {
  MenuNode node;
  node.id = std::move(id);
  node.label = std::move(label);
  node.children = std::move(children);
  return node;
}

std::wstring Trim(std::wstring value) {
  const wchar_t* whitespace = L" \t\r\n";
  const size_t first = value.find_first_not_of(whitespace);
  if (first == std::wstring::npos) return {};
  const size_t last = value.find_last_not_of(whitespace);
  return value.substr(first, last - first + 1);
}

const std::wstring* FindPreset(const ShellConfig& config, const wchar_t* key) {
  for (const auto& [name, value] : config.presets) {
    if (name == key) return &value;
  }
  return nullptr;
}

std::wstring PresetString(const ShellConfig& config, const wchar_t* key,
                          const wchar_t* fallback) {
  if (const std::wstring* raw = FindPreset(config, key)) {
    std::wstring value = Trim(*raw);
    if (!value.empty()) return value;
  }
  return fallback;
}

long long PresetBytes(const ShellConfig& config, const wchar_t* key, long long fallback) {
  if (const std::wstring* raw = FindPreset(config, key)) {
    const std::wstring value = Trim(*raw);
    if (!value.empty()) {
      const long long parsed = ::_wcstoi64(value.c_str(), nullptr, 10);
      if (parsed > 0) return parsed;
    }
  }
  return fallback;
}

std::wstring FormatBytes(long long bytes) {
  static const wchar_t* kUnits[] = {L"B", L"KB", L"MB", L"GB", L"TB", L"PB"};
  if (bytes < 1024) return std::to_wstring(bytes) + L" B";

  double scaled = static_cast<double>(bytes);
  int unit = 0;
  while (scaled >= 1024.0 && unit < static_cast<int>(std::size(kUnits)) - 1) {
    scaled /= 1024.0;
    ++unit;
  }

  const int decimals = scaled >= 100.0 ? 0 : scaled >= 10.0 ? 1 : 2;
  wchar_t buffer[64];
  ::StringCchPrintfW(buffer, ARRAYSIZE(buffer), L"%.*f", decimals, scaled);

  std::wstring text = buffer;
  if (text.find(L'.') != std::wstring::npos) {
    text.erase(text.find_last_not_of(L'0') + 1);
    if (!text.empty() && text.back() == L'.') text.pop_back();
  }
  return text + L" " + kUnits[unit];
}

MenuNode ResizeMenu(const ShellConfig& config, const LabelTable& text, const wchar_t* domain,
                    const wchar_t* prefix) {
  const std::wstring id = std::wstring(domain) + L".resize";
  const std::wstring verb = id + L"?scale=";
  const std::wstring p1 = PresetString(config, (std::wstring(prefix) + L".Resize1").c_str(), L"50");
  const std::wstring p2 = PresetString(config, (std::wstring(prefix) + L".Resize2").c_str(), L"25");
  return Popup(id, text.resize,
               {
                   Leaf(id + L".p1", p1 + L"%", verb + p1),
                   Leaf(id + L".p2", p2 + L"%", verb + p2),
                   Leaf(id + L".custom", text.custom, verb + L"custom"),
               });
}

MenuNode RotateMenu(const ShellConfig& config, const LabelTable& text, const wchar_t* domain,
                    const wchar_t* prefix) {
  const std::wstring id = std::wstring(domain) + L".rotate";
  const std::wstring verb = id + L"?deg=";
  const std::wstring p1 = PresetString(config, (std::wstring(prefix) + L".Rotate1").c_str(), L"45");
  const std::wstring p2 = PresetString(config, (std::wstring(prefix) + L".Rotate2").c_str(), L"90");
  const std::wstring p3 = PresetString(config, (std::wstring(prefix) + L".Rotate3").c_str(), L"180");
  return Popup(id, text.rotate,
               {
                   Leaf(id + L".p1", p1 + L"°", verb + p1),
                   Leaf(id + L".p2", p2 + L"°", verb + p2),
                   Leaf(id + L".p3", p3 + L"°", verb + p3),
               });
}

void AddCompress(std::vector<MenuNode>& into, const ShellConfig& config, const wchar_t* key,
                 long long fallback, const std::wstring& id, const std::wstring& verb,
                 unsigned long long maxSize) {
  const long long bytes = PresetBytes(config, key, fallback);
  if (maxSize <= static_cast<unsigned long long>(bytes)) return;
  into.push_back(Leaf(id, FormatBytes(bytes), verb + std::to_wstring(bytes)));
}

std::vector<MenuNode> ConversionItems(const std::vector<std::wstring>& allFormats,
                                      const std::set<std::wstring>& present,
                                      const wchar_t* domain) {
  std::vector<MenuNode> items;
  for (const std::wstring& format : allFormats) {
    if (present.count(format) != 0) continue;
    items.push_back(Leaf(std::wstring(domain) + L".convert." + format,
                         Label::kToPrefix + format,
                         std::wstring(domain) + L".convert?to=" + format));
  }
  return items;
}

std::vector<MenuNode> BuildFolder(const LabelTable& text) {
  return {
      Leaf(L"folder.removeDuplicate", text.removeDuplicate, L"folder.removeDuplicate"),
      Leaf(L"folder.removeEmpty", text.removeEmpty, L"folder.removeEmpty"),
      Leaf(L"folder.tree", text.tree, L"folder.tree"),
      Leaf(L"folder.rename", text.rename, L"folder.rename"),
      Leaf(L"folder.analyze", text.analyze, L"folder.analyze"),
      Leaf(L"folder.compress", text.compress, L"folder.compress"),
  };
}

std::vector<MenuNode> BuildArchive(const LabelTable& text) {
  return {Leaf(L"archive.extract", text.extract, L"archive.extract")};
}

std::vector<MenuNode> BuildPdf(const SelectionInfo& selection, const LabelTable& text) {
  std::vector<MenuNode> items;
  if (selection.paths.size() == 1) {
    items.push_back(Leaf(L"pdf.preview", text.preview, L"pdf.preview"));
  }
  if (selection.paths.size() > 1) {
    items.push_back(Leaf(L"pdf.merge", text.merge, L"pdf.merge"));
  }
  items.push_back(Leaf(L"pdf.convert.PNG", Label::kToPrefix + std::wstring(L"PNG"),
                       L"pdf.convert?to=PNG"));
  items.push_back(Leaf(L"pdf.convert.JPG", Label::kToPrefix + std::wstring(L"JPG"),
                       L"pdf.convert?to=JPG"));
  items.push_back(Leaf(L"pdf.convert.TXT", Label::kToPrefix + std::wstring(L"TXT"),
                       L"pdf.convert?to=TXT"));
  return items;
}

std::vector<MenuNode> BuildImage(const SelectionInfo& selection, const ShellConfig& config,
                                 const LabelTable& text) {
  const std::wstring verb = L"image.compress?bytes=";
  std::vector<MenuNode> compress;
  AddCompress(compress, config, L"Image.Compress1", 10 * kMiB, L"image.compress.p1", verb,
              selection.maxSize);
  AddCompress(compress, config, L"Image.Compress2", 5 * kMiB, L"image.compress.p2", verb,
              selection.maxSize);
  AddCompress(compress, config, L"Image.Compress3", 3 * kMiB, L"image.compress.p3", verb,
              selection.maxSize);
  AddCompress(compress, config, L"Image.Compress4", 1 * kMiB, L"image.compress.p4", verb,
              selection.maxSize);
  compress.push_back(Leaf(L"image.compress.custom", text.custom, verb + L"custom"));

  std::vector<MenuNode> items;
  items.push_back(ResizeMenu(config, text, L"image", L"Image"));
  items.push_back(Popup(L"image.compress", text.compress, std::move(compress)));
  items.push_back(RotateMenu(config, text, L"image", L"Image"));

  if (!selection.hasAnimatedGif) {
    std::vector<MenuNode> convert =
        ConversionItems(Sel::ImageFormats(), selection.formats, L"image");
    if (!convert.empty()) {
      items.push_back(Popup(L"image.convert", text.convert, std::move(convert)));
    }
  }
  items.push_back(Leaf(L"image.ocr", text.ocr, L"image.ocr"));
  items.push_back(Leaf(L"image.strip", text.removeMetadata, L"image.strip"));
  items.push_back(Leaf(L"image.chromakey", text.removeBackground, L"image.chromakey"));
  return items;
}

std::vector<MenuNode> BuildVideo(const SelectionInfo& selection, const ShellConfig& config,
                                 const LabelTable& text) {
  const std::wstring verb = L"video.compress?bytes=";
  std::vector<MenuNode> compress;

  const long long discord = PresetBytes(config, L"Video.CompressDiscord", 10 * kMiB);
  if (selection.maxSize > static_cast<unsigned long long>(discord)) {
    compress.push_back(Leaf(L"video.compress.discord",
                            std::wstring(text.discord) + L" (" + FormatBytes(discord) + L")",
                            L"video.compress?preset=discord"));
  }
  AddCompress(compress, config, L"Video.Compress1", 50 * kMiB, L"video.compress.p1", verb,
              selection.maxSize);
  AddCompress(compress, config, L"Video.Compress2", 25 * kMiB, L"video.compress.p2", verb,
              selection.maxSize);
  AddCompress(compress, config, L"Video.Compress3", 5 * kMiB, L"video.compress.p3", verb,
              selection.maxSize);
  compress.push_back(Leaf(L"video.compress.custom", text.custom, verb + L"custom"));

  std::vector<MenuNode> items;
  items.push_back(ResizeMenu(config, text, L"video", L"Video"));
  if (selection.paths.size() == 1) {
    items.push_back(Leaf(L"video.trim", text.trim, L"video.trim"));
    items.push_back(Leaf(L"video.thumbnail", text.thumbnail, L"video.thumbnail"));
  }
  items.push_back(Popup(L"video.compress", text.compress, std::move(compress)));
  items.push_back(RotateMenu(config, text, L"video", L"Video"));

  std::vector<MenuNode> convert =
      ConversionItems(Sel::VideoFormats(), selection.formats, L"video");
  convert.push_back(Leaf(L"video.convert.GIF", Label::kToPrefix + std::wstring(L"GIF"),
                         L"video.convert?to=GIF"));
  items.push_back(Popup(L"video.convert", text.convert, std::move(convert)));

  std::vector<MenuNode> extract;
  for (const std::wstring& format : Sel::AudioFormats()) {
    extract.push_back(Leaf(L"video.extractAudio." + format, Label::kToPrefix + format,
                           L"video.extractAudio?to=" + format));
  }
  items.push_back(Popup(L"video.extractAudio", text.extractAudio, std::move(extract)));
  return items;
}

std::vector<MenuNode> BuildAudio(const SelectionInfo& selection, const LabelTable& text) {
  std::vector<MenuNode> items;
  std::vector<MenuNode> convert =
      ConversionItems(Sel::AudioFormats(), selection.formats, L"audio");
  if (!convert.empty()) {
    items.push_back(Popup(L"audio.convert", text.convert, std::move(convert)));
  }
  return items;
}

std::vector<std::wstring> TextTargets(const std::wstring& from) {
  std::vector<std::wstring> targets;
  if (from != L"TXT") targets.push_back(L"TXT");

  if (from == L"CSV") {
    targets.insert(targets.end(), {L"JSON", L"XML", L"HTML", L"MD"});
  } else if (from == L"JSON") {
    targets.insert(targets.end(), {L"CSV", L"XML"});
  } else if (from == L"XML") {
    targets.push_back(L"JSON");
  } else if (from == L"INI" || from == L"CFG") {
    targets.insert(targets.end(), {L"JSON", L"XML"});
  }
  return targets;
}

std::vector<MenuNode> BuildText(const SelectionInfo& selection, const LabelTable& text) {
  std::vector<MenuNode> items;

  if (selection.formats.size() == 1) {
    std::vector<MenuNode> convert;
    for (const std::wstring& target : TextTargets(*selection.formats.begin())) {
      convert.push_back(Leaf(L"text.convert." + target, Label::kToPrefix + target,
                             L"text.convert?to=" + target));
    }
    if (!convert.empty()) {
      items.push_back(Popup(L"text.convert", text.convert, std::move(convert)));
    }
  }

  items.push_back(Popup(L"text.encode", text.format,
                        {
                            Leaf(L"text.encode.UTF8", Label::kToUtf8, L"text.encode?to=UTF8"),
                            Leaf(L"text.encode.UTF8BOM", Label::kToUtf8Bom,
                                 L"text.encode?to=UTF8BOM"),
                            Leaf(L"text.encode.UTF16", Label::kToUtf16, L"text.encode?to=UTF16"),
                            Leaf(L"text.encode.SJIS", Label::kToShiftJis, L"text.encode?to=SJIS"),
                        }));
  items.push_back(Popup(L"text.lineEndings", text.lineEndings,
                        {
                            Leaf(L"text.lineEndings.CRLF", Label::kToCrlf,
                                 L"text.lineEndings?to=CRLF"),
                            Leaf(L"text.lineEndings.LF", Label::kToLf, L"text.lineEndings?to=LF"),
                        }));
  if (selection.allJson) {
    items.push_back(Leaf(L"json.pretty", text.prettyPrint, L"json.pretty"));
    items.push_back(Leaf(L"json.sort", text.sortKeys, L"json.sort"));
  }
  return items;
}

std::vector<MenuNode> BuildJar(const LabelTable& text) {
  return {Leaf(L"jar.decompile", text.decompile, L"jar.decompile")};
}

std::vector<MenuNode> Prune(std::vector<MenuNode> nodes, const std::set<std::wstring>& hidden) {
  std::vector<MenuNode> kept;
  kept.reserve(nodes.size());
  for (MenuNode& node : nodes) {
    if (!node.id.empty() && hidden.count(node.id) != 0) continue;
    if (!node.children.empty()) {
      node.children = Prune(std::move(node.children), hidden);
      if (node.children.empty()) continue;
    }
    kept.push_back(std::move(node));
  }
  return kept;
}

}

std::vector<MenuNode> MenuBuilder::Build(const SelectionInfo& selection,
                                         const ShellConfig& config) {
  if (selection.Empty()) return {};

  const LabelTable& text = Labels::For(Labels::Resolve(config.language));

  std::vector<MenuNode> nodes;
  switch (selection.category) {
    case Category::Folder:
      nodes = BuildFolder(text);
      break;
    case Category::Pdf:
      nodes = BuildPdf(selection, text);
      break;
    case Category::Image:
      nodes = BuildImage(selection, config, text);
      break;
    case Category::Video:
      nodes = BuildVideo(selection, config, text);
      break;
    case Category::Audio:
      nodes = BuildAudio(selection, text);
      break;
    case Category::Text:
      nodes = BuildText(selection, text);
      break;
    case Category::Jar:
      nodes = BuildJar(text);
      break;
    case Category::Archive:
      nodes = BuildArchive(text);
      break;
    case Category::None:
    default:
      return {};
  }

  return Prune(std::move(nodes), config.hidden);
}
