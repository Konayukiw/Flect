#pragma once

#include "Context.h"
#include "WebView2.h"
#include "framework.h"

class ResourceServer {
 public:
  static constexpr const wchar_t* kAppHost = L"app.flect-preview.invalid";
  static constexpr const wchar_t* kAppUrl = L"https://app.flect-preview.invalid/index.html";

  ResourceServer(PreviewContext& context, std::wstring assets);

  void Handle(ICoreWebView2Environment* environment, ICoreWebView2WebResourceRequestedEventArgs* args);
  bool AllowsFrame(const std::wstring& uri) const;

 private:
  struct Url {
    std::wstring scheme;
    std::wstring host;
    std::wstring path;
  };

  struct Reply {
    int status = 404;
    const wchar_t* reason = L"Not Found";
    std::wstring headers;
    std::vector<uint8_t> body;
    Microsoft::WRL::ComPtr<IStream> stream;
  };

  static bool Parse(const std::wstring& uri, Url& url);
  static std::wstring Header(ICoreWebView2WebResourceRequest* request, const wchar_t* name);
  static Reply Status(int status, const wchar_t* reason);
  static std::wstring CommonHeaders();

  Reply ServeApp(const Url& url);
  Reply ServeDoc(const Url& url, ICoreWebView2WebResourceRequest* request, const std::wstring& method);
  Reply ServeSession() const;
  Reply ServeText() const;
  Reply ServeAsset(const std::wstring& relative, const std::wstring& extra, bool cors) const;
  Reply ServeFile(const std::wstring& path, const std::wstring& mime, const std::wstring& extra, const std::wstring& range) const;

  std::wstring DocOrigin() const;
  std::wstring AppPolicy() const;
  std::wstring DocPolicy() const;
  std::wstring ViewerPolicy() const;
  std::wstring ResolveProjectFile(const std::wstring& relative) const;

  PreviewContext& context_;
  std::wstring assets_;
};
