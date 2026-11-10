#pragma once

#include "Config.h"
#include "Kinds.h"
#include "Sniff.h"
#include "Trust.h"
#include "framework.h"

struct Allowances {
  bool relative = false;
  bool scripts = false;
  bool network = false;
};

struct PreviewContext {
  std::wstring path;
  std::wstring name;
  std::wstring extension;
  std::wstring root;
  std::wstring relative;
  uint64_t size = 0;
  FileType type;
  Trust::Result trust;
  Sniff::Result sniff;
  std::wstring mismatch;
  bool binary = false;
  bool remote = false;
  std::wstring docHost;
  PreviewConfig config;
  Allowances allow;
  bool resume = false;
  std::wstring convertedPath;
  std::wstring convertedRoute;
};

namespace Context {

bool Build(const std::wstring& path, PreviewContext& context);
std::wstring DataFolder();
bool HasScriptConsent(const std::wstring& path);
void GrantScriptConsent(const std::wstring& path);

}
