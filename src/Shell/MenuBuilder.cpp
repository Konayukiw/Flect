#include "MenuBuilder.h"

#include "Labels.h"

namespace {

constexpr unsigned long long kMiB = 1024ull * 1024ull;

MenuNode Leaf(std::wstring label, std::wstring verb) {
  MenuNode node;
  node.label = std::move(label);
  node.verb = std::move(verb);
  return node;
}

MenuNode Popup(std::wstring label, std::vector<MenuNode> children) {
  MenuNode node;
  node.label = std::move(label);
  node.children = std::move(children);
  return node;
}

MenuNode ResizeMenu(const wchar_t* domain) {
  const std::wstring verb = std::wstring(domain) + L".resize?scale=";
  return Popup(Label::kResize, {
                                   Leaf(Label::kResize50, verb + L"50"),
                                   Leaf(Label::kResize25, verb + L"25"),
                                   Leaf(Label::kCustom, verb + L"custom"),
                               });
}

MenuNode RotateMenu(const wchar_t* domain) {
  const std::wstring verb = std::wstring(domain) + L".rotate?deg=";
  return Popup(Label::kRotate, {
                                   Leaf(Label::kRotate45, verb + L"45"),
                                   Leaf(Label::kRotate90, verb + L"90"),
                                   Leaf(Label::kRotate180, verb + L"180"),
                               });
}

void AddPreset(std::vector<MenuNode>& into, const wchar_t* label, unsigned long long bytes,
               unsigned long long maxSize, const wchar_t* domain) {
  if (maxSize <= bytes) return;
  into.push_back(Leaf(label, std::wstring(domain) + L".compress?bytes=" +
                                 std::to_wstring(bytes)));
}

std::vector<MenuNode> ConversionItems(const std::vector<std::wstring>& allFormats,
                                      const std::set<std::wstring>& present,
                                      const wchar_t* domain) {
  std::vector<MenuNode> items;
  for (const std::wstring& format : allFormats) {
    if (present.count(format) != 0) continue;
    items.push_back(Leaf(Label::kToPrefix + format,
                         std::wstring(domain) + L".convert?to=" + format));
  }
  return items;
}

std::vector<MenuNode> BuildFolder() {
  return {
      Leaf(Label::kRemoveDuplicate, L"folder.removeDuplicate"),
      Leaf(Label::kRemoveEmpty, L"folder.removeEmpty"),
      Leaf(Label::kTree, L"folder.tree"),
      Leaf(Label::kRename, L"folder.rename"),
      Leaf(Label::kAnalyze, L"folder.analyze"),
  };
}

std::vector<MenuNode> BuildPdf(const SelectionInfo& selection) {
  std::vector<MenuNode> items;
  if (selection.paths.size() == 1) {
    items.push_back(Leaf(Label::kPreview, L"pdf.preview"));
  }
  if (selection.paths.size() > 1) {
    items.push_back(Leaf(Label::kMerge, L"pdf.merge"));
  }
  items.push_back(Leaf(Label::kToPrefix + std::wstring(L"PNG"), L"pdf.convert?to=PNG"));
  items.push_back(Leaf(Label::kToPrefix + std::wstring(L"JPG"), L"pdf.convert?to=JPG"));
  items.push_back(Leaf(Label::kToPrefix + std::wstring(L"TXT"), L"pdf.convert?to=TXT"));
  return items;
}

std::vector<MenuNode> BuildImage(const SelectionInfo& selection) {
  std::vector<MenuNode> compress;
  AddPreset(compress, Label::kSize10MB, 10 * kMiB, selection.maxSize, L"image");
  AddPreset(compress, Label::kSize5MB, 5 * kMiB, selection.maxSize, L"image");
  AddPreset(compress, Label::kSize3MB, 3 * kMiB, selection.maxSize, L"image");
  AddPreset(compress, Label::kSize1MB, 1 * kMiB, selection.maxSize, L"image");
  compress.push_back(Leaf(Label::kCustom, L"image.compress?bytes=custom"));

  std::vector<MenuNode> items;
  items.push_back(ResizeMenu(L"image"));
  items.push_back(Popup(Label::kCompress, std::move(compress)));
  items.push_back(RotateMenu(L"image"));

  if (!selection.hasAnimatedGif) {
    std::vector<MenuNode> convert =
        ConversionItems(Sel::ImageFormats(), selection.formats, L"image");
    if (!convert.empty()) {
      items.push_back(Popup(Label::kConvert, std::move(convert)));
    }
  }
  items.push_back(Leaf(Label::kOcr, L"image.ocr"));
  items.push_back(Leaf(Label::kRemoveMetadata, L"image.strip"));
  items.push_back(Leaf(Label::kRemoveGreenScreen, L"image.chromakey"));
  return items;
}

std::vector<MenuNode> BuildVideo(const SelectionInfo& selection) {
  std::vector<MenuNode> compress;
  if (selection.maxSize > 10 * kMiB) {
    compress.push_back(Leaf(Label::kDiscord, L"video.compress?preset=discord"));
  }
  AddPreset(compress, Label::kSize50MB, 50 * kMiB, selection.maxSize, L"video");
  AddPreset(compress, Label::kSize25MB, 25 * kMiB, selection.maxSize, L"video");
  AddPreset(compress, Label::kSize5MB, 5 * kMiB, selection.maxSize, L"video");
  compress.push_back(Leaf(Label::kCustom, L"video.compress?bytes=custom"));

  std::vector<MenuNode> items;
  items.push_back(ResizeMenu(L"video"));
  items.push_back(Popup(Label::kCompress, std::move(compress)));
  items.push_back(RotateMenu(L"video"));

  std::vector<MenuNode> convert =
      ConversionItems(Sel::VideoFormats(), selection.formats, L"video");
  convert.push_back(Leaf(Label::kToPrefix + std::wstring(L"GIF"), L"video.convert?to=GIF"));
  items.push_back(Popup(Label::kConvert, std::move(convert)));

  std::vector<MenuNode> extract;
  for (const std::wstring& format : Sel::AudioFormats()) {
    extract.push_back(Leaf(Label::kToPrefix + format,
                           L"video.extractAudio?to=" + format));
  }
  items.push_back(Popup(Label::kExtractAudio, std::move(extract)));
  return items;
}

std::vector<MenuNode> BuildAudio(const SelectionInfo& selection) {
  std::vector<MenuNode> items;
  std::vector<MenuNode> convert =
      ConversionItems(Sel::AudioFormats(), selection.formats, L"audio");
  if (!convert.empty()) {
    items.push_back(Popup(Label::kConvert, std::move(convert)));
  }
  return items;
}

std::vector<std::wstring> TextTargets(const std::wstring& from) {
  if (from == L"CSV") return {L"JSON", L"XML", L"HTML", L"MD"};
  if (from == L"JSON") return {L"CSV", L"XML"};
  if (from == L"XML") return {L"JSON"};
  if (from == L"INI") return {L"JSON", L"XML"};
  if (from == L"CSS" || from == L"CFG") return {L"TXT"};
  return {};
}

std::vector<MenuNode> BuildText(const SelectionInfo& selection) {
  std::vector<MenuNode> items;

  if (selection.formats.size() == 1) {
    std::vector<MenuNode> convert;
    for (const std::wstring& target : TextTargets(*selection.formats.begin())) {
      convert.push_back(Leaf(Label::kToPrefix + target, L"text.convert?to=" + target));
    }
    if (!convert.empty()) {
      items.push_back(Popup(Label::kConvert, std::move(convert)));
    }
  }

  items.push_back(Popup(Label::kFormat,
                        {
                            Leaf(Label::kToUtf8, L"text.encode?to=UTF8"),
                            Leaf(Label::kToUtf8Bom, L"text.encode?to=UTF8BOM"),
                            Leaf(Label::kToUtf16, L"text.encode?to=UTF16"),
                            Leaf(Label::kToShiftJis, L"text.encode?to=SJIS"),
                        }));
  items.push_back(Popup(Label::kLineEndings,
                        {
                            Leaf(Label::kToCrlf, L"text.lineEndings?to=CRLF"),
                            Leaf(Label::kToLf, L"text.lineEndings?to=LF"),
                        }));
  if (selection.allJson) {
    items.push_back(Leaf(Label::kPrettyPrint, L"json.pretty"));
    items.push_back(Leaf(Label::kSortKeys, L"json.sort"));
  }
  return items;
}

std::vector<MenuNode> BuildJar() {
  return {Leaf(Label::kDecompile, L"jar.decompile")};
}

}

std::vector<MenuNode> MenuBuilder::Build(const SelectionInfo& selection) {
  if (selection.Empty()) return {};
  switch (selection.category) {
    case Category::Folder:
      return BuildFolder();
    case Category::Pdf:
      return BuildPdf(selection);
    case Category::Image:
      return BuildImage(selection);
    case Category::Video:
      return BuildVideo(selection);
    case Category::Audio:
      return BuildAudio(selection);
    case Category::Text:
      return BuildText(selection);
    case Category::Jar:
      return BuildJar();
    case Category::None:
    default:
      return {};
  }
}
