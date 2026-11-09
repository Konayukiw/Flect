#pragma once

#include <optional>

#include "Context.h"
#include "Media.h"
#include "ResourceServer.h"
#include "WebView2.h"
#include "framework.h"

class Session : public std::enable_shared_from_this<Session> {
 public:
  static std::shared_ptr<Session> Create(HWND parent, const RECT& rect, const std::wstring& path, Microsoft::WRL::ComPtr<IPreviewHandlerFrame> frame, std::optional<COLORREF> background);
  ~Session();

  void Close();
  void SetRect(const RECT& rect);
  void SetParent(HWND parent, const RECT& rect);
  void Focus();
  HWND Window() const { return window_; }

 private:
  Session() = default;

  static LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam);
  static void RegisterWindowClass();

  LRESULT OnMessage(UINT message, WPARAM wparam, LPARAM lparam);
  void Paint();
  void ShowNotice(const std::wstring& text);
  void Begin();
  void CreateController(ICoreWebView2Environment* environment);
  void OnControllerReady(ICoreWebView2Environment* environment, ICoreWebView2Controller* controller);
  void ConfigureSettings();
  void AttachEvents();
  void Discard();
  void ApplyBounds();
  void OnCommand(const std::wstring& command);
  void OnConverted(bool success);
  void Post(const std::wstring& json);
  bool Forward(UINT virtualKey, bool system);

  PreviewContext context_;
  std::unique_ptr<ResourceServer> server_;
  std::unique_ptr<Conversion> conversion_;
  Microsoft::WRL::ComPtr<IPreviewHandlerFrame> frame_;
  Microsoft::WRL::ComPtr<ICoreWebView2Environment> environment_;
  Microsoft::WRL::ComPtr<ICoreWebView2Controller> controller_;
  Microsoft::WRL::ComPtr<ICoreWebView2> view_;
  HWND window_ = nullptr;
  HBRUSH brush_ = nullptr;
  RECT rect_{};
  COLORREF background_ = RGB(255, 255, 255);
  std::wstring notice_;
  int restarts_ = 0;
  bool closed_ = false;
};
