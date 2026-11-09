#pragma once

#include "framework.h"

class Conversion {
 public:
  static std::unique_ptr<Conversion> Start(HWND window, UINT message, std::wstring input, std::wstring demuxer, bool audioOnly);
  ~Conversion();

  Conversion(const Conversion&) = delete;
  Conversion& operator=(const Conversion&) = delete;

  const std::wstring& Output() const { return output_; }

 private:
  Conversion() = default;

  void Run();

  HWND window_ = nullptr;
  UINT message_ = 0;
  std::wstring executable_;
  std::wstring input_;
  std::wstring demuxer_;
  std::wstring output_;
  bool audioOnly_ = false;
  HANDLE cancel_ = nullptr;
  std::thread worker_;
};
