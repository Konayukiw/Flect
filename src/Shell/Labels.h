#pragma once

#include "framework.h"

namespace Label {

inline constexpr const wchar_t* kToPrefix = L"to ";
inline constexpr const wchar_t* kToUtf8 = L"to UTF-8";
inline constexpr const wchar_t* kToUtf8Bom = L"to UTF-8 (BOM)";
inline constexpr const wchar_t* kToUtf16 = L"to UTF-16";
inline constexpr const wchar_t* kToShiftJis = L"to Shift_JIS";
inline constexpr const wchar_t* kToCrlf = L"to CRLF (Windows)";
inline constexpr const wchar_t* kToLf = L"to LF (Unix)";

}

enum class Lang { En, Ja, Zh };

struct LabelTable {
  const wchar_t* removeDuplicate;
  const wchar_t* removeEmpty;
  const wchar_t* tree;
  const wchar_t* rename;
  const wchar_t* analyze;
  const wchar_t* preview;
  const wchar_t* merge;
  const wchar_t* resize;
  const wchar_t* trim;
  const wchar_t* thumbnail;
  const wchar_t* compress;
  const wchar_t* extract;
  const wchar_t* rotate;
  const wchar_t* convert;
  const wchar_t* ocr;
  const wchar_t* removeMetadata;
  const wchar_t* removeBackground;
  const wchar_t* extractAudio;
  const wchar_t* discord;
  const wchar_t* lineEndings;
  const wchar_t* format;
  const wchar_t* prettyPrint;
  const wchar_t* sortKeys;
  const wchar_t* decompile;
  const wchar_t* custom;
};

namespace Labels {

inline constexpr LabelTable kEn = {
    L"Remove Duplicate...",
    L"Remove Empty",
    L"Tree",
    L"Rename...",
    L"Analyze",
    L"Preview",
    L"Merge",
    L"Resize",
    L"Trim",
    L"Thumbnail",
    L"Compress",
    L"Extract...",
    L"Rotate",
    L"Convert",
    L"OCR",
    L"Remove Metadata",
    L"Remove Background",
    L"Extract Audio",
    L"Discord",
    L"Line Endings",
    L"Format",
    L"Pretty Print",
    L"Sort Keys",
    L"Decompile",
    L"Custom...",
};

inline constexpr LabelTable kJa = {
    L"重複を削除...",
    L"空フォルダを削除",
    L"ツリー表示",
    L"名前を一括変更...",
    L"解析",
    L"プレビュー",
    L"結合", 
    L"リサイズ",
    L"トリム",
    L"サムネイル",
    L"圧縮",
    L"展開...",
    L"回転",
    L"変換",
    L"OCR",
    L"メタデータを削除",
    L"背景を削除",
    L"音声を抽出",
    L"Discord",
    L"改行コード",
    L"文字コード",
    L"整形",
    L"キーを並べ替え",
    L"逆コンパイル",
    L"カスタム...",
};

inline constexpr LabelTable kZh = {
    L"删除重复文件...",
    L"删除空文件夹",
    L"目录树",
    L"批量重命名...",
    L"分析",
    L"预览",
    L"合并",
    L"调整尺寸",
    L"剪切",
    L"缩略图",
    L"压缩",
    L"解压...",
    L"旋转",
    L"转换",
    L"文字识别",
    L"删除元数据",
    L"删除背景",
    L"提取音频",
    L"Discord",
    L"换行符",
    L"字符编码",
    L"格式化",
    L"按键排序",
    L"反编译",
    L"自定义...",
};

inline Lang DetectSystem() {
  switch (PRIMARYLANGID(::GetUserDefaultUILanguage())) {
    case LANG_JAPANESE:
      return Lang::Ja;
    case LANG_CHINESE:
      return Lang::Zh;
    default:
      return Lang::En;
  }
}

inline Lang Resolve(const std::wstring& tag) {
  if (tag == L"en") return Lang::En;
  if (tag == L"ja") return Lang::Ja;
  if (tag.rfind(L"zh", 0) == 0) return Lang::Zh;
  return DetectSystem();
}

inline const LabelTable& For(Lang language) {
  switch (language) {
    case Lang::Ja:
      return kJa;
    case Lang::Zh:
      return kZh;
    case Lang::En:
    default:
      return kEn;
  }
}

}
