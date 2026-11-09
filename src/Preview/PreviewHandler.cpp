#include "PreviewHandler.h"

#include "Module.h"

PreviewHandler::PreviewHandler() { Module::AddRef(); }

PreviewHandler::~PreviewHandler() {
  if (session_) session_->Close();
  session_.reset();
  Module::Release();
}

IFACEMETHODIMP PreviewHandler::QueryInterface(REFIID riid, void** ppv) {
  if (!ppv) return E_POINTER;
  *ppv = nullptr;
  if (riid == IID_IUnknown || riid == IID_IPreviewHandler) *ppv = static_cast<IPreviewHandler*>(this);
  else if (riid == IID_IPreviewHandlerVisuals) *ppv = static_cast<IPreviewHandlerVisuals*>(this);
  else if (riid == IID_IOleWindow) *ppv = static_cast<IOleWindow*>(this);
  else if (riid == IID_IObjectWithSite) *ppv = static_cast<IObjectWithSite*>(this);
  else if (riid == IID_IInitializeWithItem) *ppv = static_cast<IInitializeWithItem*>(this);
  else if (riid == IID_IInitializeWithFile) *ppv = static_cast<IInitializeWithFile*>(this);
  if (!*ppv) return E_NOINTERFACE;
  AddRef();
  return S_OK;
}

IFACEMETHODIMP_(ULONG) PreviewHandler::AddRef() { return static_cast<ULONG>(++refs_); }

IFACEMETHODIMP_(ULONG) PreviewHandler::Release() {
  const long remaining = --refs_;
  if (remaining == 0) delete this;
  return static_cast<ULONG>(remaining);
}

IFACEMETHODIMP PreviewHandler::Initialize(IShellItem* item, DWORD) {
  if (!item) return E_INVALIDARG;
  PWSTR raw = nullptr;
  const HRESULT hr = item->GetDisplayName(SIGDN_FILESYSPATH, &raw);
  if (FAILED(hr)) return hr;
  path_ = raw;
  ::CoTaskMemFree(raw);
  return S_OK;
}

IFACEMETHODIMP PreviewHandler::Initialize(LPCWSTR path, DWORD) {
  if (!path) return E_INVALIDARG;
  path_ = path;
  return S_OK;
}

IFACEMETHODIMP PreviewHandler::SetSite(IUnknown* site) {
  site_ = site;
  frame_.Reset();
  if (site_) site_.As(&frame_);
  return S_OK;
}

IFACEMETHODIMP PreviewHandler::GetSite(REFIID riid, void** ppv) {
  if (!ppv) return E_POINTER;
  *ppv = nullptr;
  return site_ ? site_.CopyTo(riid, ppv) : E_FAIL;
}

IFACEMETHODIMP PreviewHandler::GetWindow(HWND* window) {
  if (!window) return E_POINTER;
  *window = session_ ? session_->Window() : nullptr;
  return *window ? S_OK : E_FAIL;
}

IFACEMETHODIMP PreviewHandler::ContextSensitiveHelp(BOOL) { return E_NOTIMPL; }

IFACEMETHODIMP PreviewHandler::SetWindow(HWND parent, const RECT* rect) {
  if (!rect) return E_INVALIDARG;
  parent_ = parent;
  rect_ = *rect;
  if (session_) session_->SetParent(parent_, rect_);
  return S_OK;
}

IFACEMETHODIMP PreviewHandler::SetRect(const RECT* rect) {
  if (!rect) return E_INVALIDARG;
  rect_ = *rect;
  if (session_) session_->SetRect(rect_);
  return S_OK;
}

IFACEMETHODIMP PreviewHandler::DoPreview() {
  if (session_) Unload();
  if (path_.empty() || !parent_) return E_FAIL;
  session_ = Session::Create(parent_, rect_, path_, frame_, background_);
  return session_ ? S_OK : E_FAIL;
}

IFACEMETHODIMP PreviewHandler::Unload() {
  if (session_) session_->Close();
  session_.reset();
  return S_OK;
}

IFACEMETHODIMP PreviewHandler::SetFocus() {
  if (session_) session_->Focus();
  return S_OK;
}

IFACEMETHODIMP PreviewHandler::QueryFocus(HWND* window) {
  if (!window) return E_POINTER;
  *window = ::GetFocus();
  return *window ? S_OK : HRESULT_FROM_WIN32(::GetLastError());
}

IFACEMETHODIMP PreviewHandler::TranslateAccelerator(MSG* message) {
  return frame_ ? frame_->TranslateAccelerator(message) : S_FALSE;
}

IFACEMETHODIMP PreviewHandler::SetBackgroundColor(COLORREF color) {
  background_ = color;
  return S_OK;
}

IFACEMETHODIMP PreviewHandler::SetFont(const LOGFONTW*) { return S_OK; }

IFACEMETHODIMP PreviewHandler::SetTextColor(COLORREF) { return S_OK; }
