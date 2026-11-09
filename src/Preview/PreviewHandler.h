#pragma once

#include <optional>

#include "Session.h"
#include "framework.h"

class PreviewHandler final : public IPreviewHandler,
                             public IPreviewHandlerVisuals,
                             public IOleWindow,
                             public IObjectWithSite,
                             public IInitializeWithItem,
                             public IInitializeWithFile {
 public:
  PreviewHandler();

  IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
  IFACEMETHODIMP_(ULONG) AddRef() override;
  IFACEMETHODIMP_(ULONG) Release() override;

  IFACEMETHODIMP Initialize(IShellItem* item, DWORD mode) override;
  IFACEMETHODIMP Initialize(LPCWSTR path, DWORD mode) override;

  IFACEMETHODIMP SetSite(IUnknown* site) override;
  IFACEMETHODIMP GetSite(REFIID riid, void** ppv) override;

  IFACEMETHODIMP GetWindow(HWND* window) override;
  IFACEMETHODIMP ContextSensitiveHelp(BOOL enter) override;

  IFACEMETHODIMP SetWindow(HWND parent, const RECT* rect) override;
  IFACEMETHODIMP SetRect(const RECT* rect) override;
  IFACEMETHODIMP DoPreview() override;
  IFACEMETHODIMP Unload() override;
  IFACEMETHODIMP SetFocus() override;
  IFACEMETHODIMP QueryFocus(HWND* window) override;
  IFACEMETHODIMP TranslateAccelerator(MSG* message) override;

  IFACEMETHODIMP SetBackgroundColor(COLORREF color) override;
  IFACEMETHODIMP SetFont(const LOGFONTW* font) override;
  IFACEMETHODIMP SetTextColor(COLORREF color) override;

 private:
  ~PreviewHandler();

  std::atomic<long> refs_{1};
  std::wstring path_;
  Microsoft::WRL::ComPtr<IUnknown> site_;
  Microsoft::WRL::ComPtr<IPreviewHandlerFrame> frame_;
  std::shared_ptr<Session> session_;
  std::optional<COLORREF> background_;
  HWND parent_ = nullptr;
  RECT rect_{};
};
