#include "ResourceServer.h"

#include "Text.h"

using Microsoft::WRL::ComPtr;

namespace {

constexpr uint64_t kTextLimit = 4 * 1024 * 1024;
constexpr uint64_t kRangeChunk = 16 * 1024 * 1024;
constexpr std::wstring_view kViewerPrefix = L"/__flect/";
constexpr std::wstring_view kViewerPage = L"/__flect/viewer/pdf.html";

bool ValidSegment(std::wstring_view segment) {
  if (segment.empty() || segment.front() == L'.') return false;
  return segment.find_first_of(L"\\:*?\"<>|") == std::wstring_view::npos;
}

bool ValidRelative(std::wstring_view path) {
  size_t start = 0;
  while (start <= path.size()) {
    const size_t slash = path.find(L'/', start);
    const size_t stop = slash == std::wstring_view::npos ? path.size() : slash;
    if (!ValidSegment(path.substr(start, stop - start))) return false;
    if (slash == std::wstring_view::npos) break;
    start = slash + 1;
  }
  return !path.empty();
}

bool WithoutLinks(const std::wstring& root, const std::wstring& relative) {
  std::wstring current = root;
  size_t start = 0;
  while (start < relative.size()) {
    const size_t separator = relative.find(L'\\', start);
    const size_t stop = separator == std::wstring::npos ? relative.size() : separator;
    current += L'\\';
    current.append(relative, start, stop - start);
    WIN32_FIND_DATAW entry{};
    HANDLE search = ::FindFirstFileExW(current.c_str(), FindExInfoBasic, &entry, FindExSearchNameMatch, nullptr, 0);
    if (search == INVALID_HANDLE_VALUE) return false;
    ::FindClose(search);
    if ((entry.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) && IsReparseTagNameSurrogate(entry.dwReserved0)) return false;
    if (separator == std::wstring::npos) break;
    start = separator + 1;
  }
  return true;
}

bool ParseRange(std::wstring_view header, uint64_t size, uint64_t& begin, uint64_t& end) {
  constexpr std::wstring_view prefix = L"bytes=";
  if (header.substr(0, prefix.size()) != prefix || size == 0) return false;
  const std::wstring_view spec = header.substr(prefix.size());
  const size_t dash = spec.find(L'-');
  if (dash == std::wstring_view::npos || spec.find(L',') != std::wstring_view::npos) return false;

  const std::wstring first(spec.substr(0, dash));
  const std::wstring last(spec.substr(dash + 1));
  if (first.empty()) {
    if (last.empty()) return false;
    const uint64_t tail = std::min<uint64_t>(_wcstoui64(last.c_str(), nullptr, 10), size);
    begin = size - tail;
    end = size - 1;
    return tail != 0;
  }
  begin = _wcstoui64(first.c_str(), nullptr, 10);
  if (begin >= size) return false;
  end = last.empty() ? size - 1 : std::min<uint64_t>(_wcstoui64(last.c_str(), nullptr, 10), size - 1);
  return end >= begin;
}

bool ReadBytes(const std::wstring& path, uint64_t offset, uint64_t length, std::vector<uint8_t>& output) {
  HANDLE file = ::CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING,
                              FILE_ATTRIBUTE_NORMAL, nullptr);
  if (file == INVALID_HANDLE_VALUE) return false;
  LARGE_INTEGER position{};
  position.QuadPart = static_cast<LONGLONG>(offset);
  bool ok = ::SetFilePointerEx(file, position, nullptr, FILE_BEGIN) != FALSE;
  output.resize(static_cast<size_t>(length));
  size_t total = 0;
  while (ok && total < output.size()) {
    DWORD read = 0;
    const DWORD request = static_cast<DWORD>(std::min<size_t>(output.size() - total, 1 << 24));
    ok = ::ReadFile(file, output.data() + total, request, &read, nullptr) != FALSE;
    if (ok && read == 0) break;
    total += read;
  }
  ::CloseHandle(file);
  output.resize(total);
  return ok;
}

bool FileSize(const std::wstring& path, uint64_t& size) {
  WIN32_FILE_ATTRIBUTE_DATA data{};
  if (!::GetFileAttributesExW(path.c_str(), GetFileExInfoStandard, &data)) return false;
  if (data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) return false;
  size = static_cast<uint64_t>(data.nFileSizeHigh) << 32 | data.nFileSizeLow;
  return true;
}

std::wstring Number(uint64_t value) { return std::to_wstring(value); }

std::wstring AnsiEncoding() {
  const UINT page = ::GetACP();
  switch (page) {
    case 932: return L"shift_jis";
    case 936:
    case 54936: return L"gb18030";
    case 949: return L"euc-kr";
    case 950: return L"big5";
    case 874: return L"windows-874";
    default:
      return page >= 1250 && page <= 1258 ? L"windows-" + std::to_wstring(page) : L"windows-1252";
  }
}

const wchar_t* Bool(bool value) { return value ? L"true" : L"false"; }

std::wstring CorsHeaders() {
  return L"Access-Control-Allow-Origin: *\r\nAccess-Control-Expose-Headers: Accept-Ranges, Content-Range, Content-Length\r\nCross-Origin-Resource-Policy: cross-origin\r\n";
}

}

ResourceServer::ResourceServer(PreviewContext& context, std::wstring assets) : context_(context), assets_(std::move(assets)) {}

bool ResourceServer::Parse(const std::wstring& uri, Url& url) {
  const size_t scheme = uri.find(L':');
  if (scheme == std::wstring::npos) return false;
  url.scheme = Text::Lower(std::wstring_view(uri).substr(0, scheme));
  if (uri.compare(scheme, 3, L"://") != 0) return true;
  const size_t hostStart = scheme + 3;
  const size_t hostEnd = uri.find_first_of(L"/?#", hostStart);
  url.host = Text::Lower(std::wstring_view(uri).substr(hostStart, hostEnd == std::wstring::npos ? std::wstring::npos : hostEnd - hostStart));
  if (hostEnd == std::wstring::npos || uri[hostEnd] != L'/') {
    url.path = L"/";
    return true;
  }
  const size_t pathEnd = uri.find_first_of(L"?#", hostEnd);
  url.path = uri.substr(hostEnd, pathEnd == std::wstring::npos ? std::wstring::npos : pathEnd - hostEnd);
  return true;
}

std::wstring ResourceServer::Header(ICoreWebView2WebResourceRequest* request, const wchar_t* name) {
  ComPtr<ICoreWebView2HttpRequestHeaders> headers;
  if (FAILED(request->get_Headers(&headers)) || !headers) return {};
  BOOL present = FALSE;
  if (FAILED(headers->Contains(name, &present)) || !present) return {};
  LPWSTR raw = nullptr;
  if (FAILED(headers->GetHeader(name, &raw))) return {};
  return Text::Take(raw);
}

ResourceServer::Reply ResourceServer::Status(int status, const wchar_t* reason) {
  Reply reply;
  reply.status = status;
  reply.reason = reason;
  reply.headers = CommonHeaders();
  return reply;
}

std::wstring ResourceServer::CommonHeaders() {
  return L"Cache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\n";
}

std::wstring ResourceServer::DocOrigin() const { return L"https://" + context_.docHost; }

std::wstring ResourceServer::AppPolicy() const {
  const std::wstring doc = DocOrigin();
  return L"default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; font-src 'self' data: " + doc + L"; img-src 'self' data: " + doc +
         L"; media-src " + doc + L"; connect-src 'self' " + doc + L"; frame-src " + doc + L"; worker-src 'self' blob:; base-uri 'none'; form-action 'none'";
}

std::wstring ResourceServer::DocPolicy() const {
  const std::wstring doc = DocOrigin();
  if (context_.allow.network) {
    return L"default-src * data: blob: 'unsafe-inline' 'unsafe-eval'; frame-src 'none'; form-action 'none'; sandbox allow-scripts";
  }
  if (context_.allow.scripts) {
    return L"default-src 'none'; script-src " + doc + L" 'unsafe-inline' 'unsafe-eval' blob:; style-src " + doc + L" 'unsafe-inline'; img-src " + doc +
           L" data: blob:; font-src " + doc + L" data:; media-src " + doc + L" blob:; connect-src " + doc + L"; worker-src " + doc +
           L" blob:; base-uri " + doc + L"; form-action 'none'; frame-src 'none'; sandbox allow-scripts";
  }
  if (context_.allow.relative) {
    return L"default-src 'none'; style-src " + doc + L" 'unsafe-inline'; img-src " + doc + L" data:; font-src " + doc + L" data:; media-src " + doc +
           L"; base-uri " + doc + L"; form-action 'none'; frame-src 'none'; sandbox";
  }
  return L"default-src 'none'; style-src 'unsafe-inline'; img-src data:; font-src data:; base-uri 'none'; form-action 'none'; frame-src 'none'; sandbox";
}

std::wstring ResourceServer::ViewerPolicy() const {
  const std::wstring doc = DocOrigin();
  return L"default-src 'none'; script-src " + doc + L" 'wasm-unsafe-eval'; style-src " + doc + L" 'unsafe-inline'; img-src blob: data:; font-src " + doc +
         L" data: blob:; connect-src " + doc + L"; worker-src blob:; base-uri 'none'; form-action 'none'; frame-src 'none'; sandbox allow-scripts";
}

bool ResourceServer::AllowsFrame(const std::wstring& uri) const {
  if (uri == L"about:blank" || uri == L"about:srcdoc") return true;
  Url url;
  if (!Parse(uri, url) || url.scheme != L"https" || url.host != context_.docHost) return false;
  const std::wstring decoded = Text::DecodePath(url.path);
  if (decoded == kViewerPage) return true;
  return Text::SameFold(std::wstring_view(decoded).substr(1), context_.relative);
}

std::wstring ResourceServer::ResolveProjectFile(const std::wstring& relative) const {
  if (context_.remote || !ValidRelative(relative)) return {};
  std::wstring inside = relative;
  std::replace(inside.begin(), inside.end(), L'/', L'\\');
  if (!WithoutLinks(context_.root, inside)) return {};
  const std::wstring path = context_.root + L"\\" + inside;
  uint64_t size = 0;
  if (!FileSize(path, size)) return {};
  const std::wstring root = Trust::FinalPath(context_.root);
  const std::wstring final = Trust::FinalPath(path);
  if (root.empty() || final.size() <= root.size() + 1 || final[root.size()] != L'\\') return {};
  if (!Text::SameFold(std::wstring_view(final).substr(0, root.size()), root)) return {};
  return path;
}

ResourceServer::Reply ResourceServer::ServeFile(const std::wstring& path, const std::wstring& mime, const std::wstring& extra, const std::wstring& range) const {
  uint64_t size = 0;
  if (!FileSize(path, size)) return Status(404, L"Not Found");

  std::wstring headers = CommonHeaders() + extra + L"Content-Type: " + mime + L"\r\nAccept-Ranges: bytes\r\n";

  if (!range.empty()) {
    uint64_t begin = 0;
    uint64_t end = 0;
    if (!ParseRange(range, size, begin, end)) {
      Reply reply = Status(416, L"Range Not Satisfiable");
      reply.headers = headers + L"Content-Range: bytes */" + Number(size) + L"\r\n";
      return reply;
    }
    end = std::min<uint64_t>(end, begin + kRangeChunk - 1);
    Reply reply;
    if (!ReadBytes(path, begin, end - begin + 1, reply.body)) return Status(500, L"Read Failed");
    reply.status = 206;
    reply.reason = L"Partial Content";
    reply.headers = headers + L"Content-Range: bytes " + Number(begin) + L"-" + Number(end) + L"/" + Number(size) + L"\r\nContent-Length: " + Number(end - begin + 1) + L"\r\n";
    return reply;
  }

  Reply reply;
  if (FAILED(::SHCreateStreamOnFileEx(path.c_str(), STGM_READ | STGM_SHARE_DENY_NONE, FILE_ATTRIBUTE_NORMAL, FALSE, nullptr, &reply.stream))) return Status(500, L"Read Failed");
  reply.status = 200;
  reply.reason = L"OK";
  reply.headers = headers + L"Content-Length: " + Number(size) + L"\r\n";
  return reply;
}

ResourceServer::Reply ResourceServer::ServeAsset(const std::wstring& relative, const std::wstring& extra, bool cors) const {
  if (!ValidRelative(relative)) return Status(403, L"Forbidden");
  std::wstring path = relative;
  std::replace(path.begin(), path.end(), L'/', L'\\');
  const std::wstring extension = ::PathFindExtensionW(path.c_str());
  return ServeFile(assets_ + L"\\" + path, Kinds::Mime(extension), (cors ? CorsHeaders() : std::wstring{}) + extra, L"");
}

ResourceServer::Reply ResourceServer::ServeSession() const {
  const PreviewContext& c = context_;
  const bool restricted = c.trust.level == Trust::Level::Restricted;
  const std::wstring doc = DocOrigin();
  const std::wstring encoded = Text::EncodePath(c.relative);
  const size_t lastSlash = encoded.find_last_of(L'/');
  const std::wstring folder = lastSlash == std::wstring::npos ? std::wstring{} : encoded.substr(0, lastSlash + 1);

  std::wstring json = L"{";
  json += L"\"name\":" + Text::Json(c.name);
  json += L",\"size\":" + Number(c.size);
  json += L",\"kind\":" + Text::Json(Kinds::Name(c.type.kind));
  json += L",\"language\":" + Text::Json(c.type.language);
  json += L",\"extension\":" + Text::Json(c.extension);
  json += L",\"trust\":" + Text::Json(Trust::Name(c.trust.level));
  json += L",\"marked\":" + std::wstring(Bool(c.trust.marked));
  json += L",\"zone\":" + std::to_wstring(c.trust.zone);
  json += L",\"mismatch\":" + Text::Json(c.mismatch);
  json += L",\"binary\":" + std::wstring(Bool(c.binary));
  json += L",\"truncated\":" + std::wstring(Bool(c.size > kTextLimit));
  json += L",\"docOrigin\":" + Text::Json(doc);
  json += L",\"docUrl\":" + Text::Json(doc + L"/" + encoded);
  json += L",\"docBase\":" + Text::Json(doc + L"/" + folder);
  json += L",\"viewerBase\":" + Text::Json(doc + std::wstring(kViewerPrefix));
  json += L",\"theme\":" + Text::Json(c.config.darkTheme ? L"dark" : L"light");
  json += L",\"locale\":" + Text::Json(c.config.locale);
  json += L",\"fallbackEncoding\":" + Text::Json(AnsiEncoding());
  json += L",\"fontFamily\":" + Text::Json(c.config.fontFamily);
  json += L",\"fontSize\":" + std::to_wstring(c.config.fontSize);
  json += L",\"wordWrap\":" + std::wstring(Bool(c.config.wordWrap));
  json += L",\"sample\":" + Text::Json(c.config.sampleText);
  json += L",\"scripts\":" + std::wstring(Bool(c.allow.scripts));
  json += L",\"relative\":" + std::wstring(Bool(c.allow.relative));
  json += L",\"network\":" + std::wstring(Bool(c.allow.network));
  json += L",\"resume\":" + std::wstring(Bool(c.resume));
  json += L",\"canAllowScripts\":" + std::wstring(Bool(!restricted && c.trust.level == Trust::Level::Normal));
  json += L",\"canAllowNetwork\":" + std::wstring(Bool(!restricted && !c.trust.marked));
  json += L",\"canConvert\":" + std::wstring(Bool(!restricted && !c.trust.marked && !c.sniff.demuxer.empty() && c.mismatch.empty()));
  json += L"}";

  Reply reply;
  reply.status = 200;
  reply.reason = L"OK";
  reply.headers = CommonHeaders() + L"Content-Type: application/json; charset=utf-8\r\n";
  const std::string bytes = Text::ToUtf8(json);
  reply.body.assign(bytes.begin(), bytes.end());
  return reply;
}

ResourceServer::Reply ResourceServer::ServeText() const {
  if (context_.binary) return Status(415, L"Unsupported");
  Reply reply;
  if (!ReadBytes(context_.path, 0, std::min(context_.size, kTextLimit), reply.body)) return Status(500, L"Read Failed");
  reply.status = 200;
  reply.reason = L"OK";
  reply.headers = CommonHeaders() + L"Content-Type: application/octet-stream\r\n";
  return reply;
}

ResourceServer::Reply ResourceServer::ServeApp(const Url& url) {
  const std::wstring path = Text::DecodePath(url.path);
  if (path == L"/" || path == L"/index.html") {
    return ServeAsset(L"index.html", L"Content-Security-Policy: " + AppPolicy() + L"\r\n", false);
  }
  if (path == L"/api/session") return ServeSession();
  if (path == L"/api/text") return ServeText();
  return ServeAsset(path.substr(1), L"", false);
}

ResourceServer::Reply ResourceServer::ServeDoc(const Url& url, ICoreWebView2WebResourceRequest* request, const std::wstring& method) {
  if (method == L"OPTIONS") {
    Reply reply = Status(204, L"No Content");
    reply.headers += CorsHeaders() + L"Access-Control-Allow-Methods: GET, HEAD\r\nAccess-Control-Allow-Headers: Range\r\n";
    return reply;
  }
  if (method != L"GET" && method != L"HEAD") return Status(405, L"Method Not Allowed");

  const std::wstring decoded = Text::DecodePath(url.path);
  const std::wstring range = Header(request, L"Range");
  const std::wstring_view view(decoded);

  if (view.substr(0, kViewerPrefix.size()) == kViewerPrefix) {
    const std::wstring route = decoded.substr(kViewerPrefix.size());
    if (!context_.convertedRoute.empty() && route == context_.convertedRoute) {
      const std::wstring extension = ::PathFindExtensionW(context_.convertedPath.c_str());
      return ServeFile(context_.convertedPath, Kinds::Mime(extension), CorsHeaders(), range);
    }
    return ServeAsset(route, L"Content-Security-Policy: " + ViewerPolicy() + L"\r\n", true);
  }

  const std::wstring relative = decoded.substr(1);
  const bool target = Text::SameFold(relative, context_.relative);
  if (!target && !context_.allow.relative) return Status(403, L"Forbidden");

  std::wstring file = context_.path;
  std::wstring extension = context_.extension;
  if (!target) {
    file = ResolveProjectFile(relative);
    if (file.empty()) return Status(404, L"Not Found");
    extension = ::PathFindExtensionW(file.c_str());
  }
  return ServeFile(file, Kinds::Mime(extension), CorsHeaders() + L"Content-Security-Policy: " + DocPolicy() + L"\r\n", range);
}

void ResourceServer::Handle(ICoreWebView2Environment* environment, ICoreWebView2WebResourceRequestedEventArgs* args) {
  ComPtr<ICoreWebView2WebResourceRequest> request;
  if (FAILED(args->get_Request(&request)) || !request) return;

  LPWSTR raw = nullptr;
  if (FAILED(request->get_Uri(&raw))) return;
  const std::wstring uri = Text::Take(raw);
  raw = nullptr;
  if (FAILED(request->get_Method(&raw))) return;
  const std::wstring method = Text::Take(raw);

  Url url;
  if (!Parse(uri, url)) return;
  if (url.scheme == L"blob" || url.scheme == L"data" || url.scheme == L"about") return;

  Reply reply;
  if (url.scheme == L"https" && url.host == kAppHost) {
    reply = ServeApp(url);
  } else if (url.scheme == L"https" && url.host == context_.docHost) {
    reply = ServeDoc(url, request.Get(), method);
  } else if (context_.allow.network && (url.scheme == L"https" || url.scheme == L"http")) {
    return;
  } else {
    reply = Status(403, L"Forbidden");
  }

  ComPtr<IStream> stream = reply.stream;
  if (!stream && !reply.body.empty()) {
    stream.Attach(::SHCreateMemStream(reply.body.data(), static_cast<UINT>(reply.body.size())));
  }

  ComPtr<ICoreWebView2WebResourceResponse> response;
  if (SUCCEEDED(environment->CreateWebResourceResponse(stream.Get(), reply.status, reply.reason, reply.headers.c_str(), &response))) {
    args->put_Response(response.Get());
  }
}
