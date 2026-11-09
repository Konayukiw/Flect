#include "Session.h"

#include "Module.h"
#include "PreviewStrings.g.h"
#include "Text.h"
#include "WebView2EnvironmentOptions.h"

using Microsoft::WRL::Callback;
using Microsoft::WRL::ComPtr;
using Microsoft::WRL::Make;

namespace {

constexpr wchar_t kWindowClass[] = L"FlectPreviewHost";
constexpr UINT kBeginMessage = WM_APP + 1;
constexpr UINT kRestartMessage = WM_APP + 2;
constexpr UINT kConvertedMessage = WM_APP + 3;
constexpr int kMaximumRestarts = 3;

constexpr wchar_t kCommonArguments[] =
    L"--disable-background-networking --disable-component-update --disable-sync --disable-domain-reliability --no-pings "
    L"--force-webrtc-ip-handling-policy=disable_non_proxied_udp";
constexpr wchar_t kOfflineArguments[] = L" --proxy-server=127.0.0.1:9 --proxy-bypass-list=<-loopback> --host-resolver-rules=\"MAP * ~NOTFOUND\"";

thread_local ICoreWebView2Environment* t_environments[2] = {nullptr, nullptr};

void Remember(int slot, ICoreWebView2Environment* environment) {
  if (t_environments[slot]) t_environments[slot]->Release();
  t_environments[slot] = environment;
  if (environment) environment->AddRef();
}

const wchar_t* RuntimeNotice(const std::wstring& locale) {
  if (locale == L"ja") return PreviewStrings::kRuntimeNoticeJa;
  if (locale == L"zh") return PreviewStrings::kRuntimeNoticeZh;
  return PreviewStrings::kRuntimeNoticeEn;
}

}

void Session::RegisterWindowClass() {
  static const ATOM atom = [] {
    WNDCLASSW type{};
    type.lpfnWndProc = WindowProc;
    type.hInstance = Module::Instance();
    type.lpszClassName = kWindowClass;
    type.hCursor = ::LoadCursorW(nullptr, IDC_ARROW);
    return ::RegisterClassW(&type);
  }();
  (void)atom;
}

LRESULT CALLBACK Session::WindowProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) {
  if (message == WM_NCCREATE) {
    const auto* create = reinterpret_cast<const CREATESTRUCTW*>(lparam);
    ::SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(create->lpCreateParams));
    static_cast<Session*>(create->lpCreateParams)->window_ = window;
  }
  auto* session = reinterpret_cast<Session*>(::GetWindowLongPtrW(window, GWLP_USERDATA));
  if (session) return session->OnMessage(message, wparam, lparam);
  return ::DefWindowProcW(window, message, wparam, lparam);
}

std::shared_ptr<Session> Session::Create(HWND parent, const RECT& rect, const std::wstring& path, ComPtr<IPreviewHandlerFrame> frame, std::optional<COLORREF> background) {
  std::shared_ptr<Session> session(new (std::nothrow) Session());
  if (!session) return nullptr;
  if (!Context::Build(path, session->context_)) return nullptr;

  if (background) {
    const int luminance = (GetRValue(*background) * 299 + GetGValue(*background) * 587 + GetBValue(*background) * 114) / 1000;
    session->context_.config.darkTheme = luminance < 128;
    session->background_ = *background;
  } else {
    session->background_ = session->context_.config.darkTheme ? RGB(30, 30, 30) : RGB(255, 255, 255);
  }

  session->frame_ = std::move(frame);
  session->rect_ = rect;
  session->brush_ = ::CreateSolidBrush(session->background_);
  session->server_ = std::make_unique<ResourceServer>(session->context_, Module::Directory() + L"\\preview\\web");

  RegisterWindowClass();
  session->window_ = ::CreateWindowExW(0, kWindowClass, L"", WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, parent, nullptr, Module::Instance(), session.get());
  if (!session->window_) return nullptr;
  ::PostMessageW(session->window_, kBeginMessage, 0, 0);
  return session;
}

Session::~Session() {
  Close();
  if (brush_) ::DeleteObject(brush_);
}

void Session::Close() {
  if (closed_) return;
  closed_ = true;
  conversion_.reset();
  Discard();
  environment_.Reset();
  if (window_) {
    ::SetWindowLongPtrW(window_, GWLP_USERDATA, 0);
    ::DestroyWindow(window_);
    window_ = nullptr;
  }
}

void Session::Discard() {
  if (controller_) controller_->Close();
  controller_.Reset();
  view_.Reset();
}

void Session::SetRect(const RECT& rect) {
  rect_ = rect;
  if (!window_) return;
  ::SetWindowPos(window_, nullptr, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, SWP_NOZORDER | SWP_NOACTIVATE);
  ApplyBounds();
}

void Session::SetParent(HWND parent, const RECT& rect) {
  if (window_) ::SetParent(window_, parent);
  SetRect(rect);
}

void Session::ApplyBounds() {
  if (!controller_) return;
  const RECT bounds{0, 0, rect_.right - rect_.left, rect_.bottom - rect_.top};
  controller_->put_Bounds(bounds);
}

void Session::Focus() {
  if (controller_) controller_->MoveFocus(COREWEBVIEW2_MOVE_FOCUS_REASON_PROGRAMMATIC);
}

void Session::ShowNotice(const std::wstring& text) {
  notice_ = text;
  if (window_) ::InvalidateRect(window_, nullptr, TRUE);
}

void Session::Paint() {
  PAINTSTRUCT paint{};
  HDC device = ::BeginPaint(window_, &paint);
  RECT client{};
  ::GetClientRect(window_, &client);
  ::FillRect(device, &client, brush_);
  if (!notice_.empty()) {
    ::SetBkMode(device, TRANSPARENT);
    ::SetTextColor(device, context_.config.darkTheme ? RGB(230, 230, 230) : RGB(32, 32, 32));
    ::SelectObject(device, ::GetStockObject(DEFAULT_GUI_FONT));
    ::InflateRect(&client, -16, -16);
    ::DrawTextW(device, notice_.c_str(), -1, &client, DT_CENTER | DT_VCENTER | DT_WORDBREAK);
  }
  ::EndPaint(window_, &paint);
}

LRESULT Session::OnMessage(UINT message, WPARAM wparam, LPARAM lparam) {
  switch (message) {
    case kBeginMessage:
      Begin();
      return 0;
    case kRestartMessage:
      Discard();
      Begin();
      return 0;
    case kConvertedMessage:
      OnConverted(wparam != 0);
      return 0;
    case WM_PAINT:
      Paint();
      return 0;
    case WM_ERASEBKGND:
      return 1;
    default:
      return ::DefWindowProcW(window_, message, wparam, lparam);
  }
}

void Session::Begin() {
  const int slot = context_.allow.network ? 1 : 0;
  if (t_environments[slot]) {
    CreateController(t_environments[slot]);
    return;
  }

  const std::wstring root = Context::DataFolder();
  if (root.empty()) {
    ShowNotice(RuntimeNotice(context_.config.locale));
    return;
  }
  const std::wstring folder = root + (slot == 1 ? L"\\WebViewOnline" : L"\\WebView");

  auto options = Make<CoreWebView2EnvironmentOptions>();
  std::wstring arguments = kCommonArguments;
  if (slot == 0) arguments += kOfflineArguments;
  options->put_AdditionalBrowserArguments(arguments.c_str());
  options->put_AllowSingleSignOnUsingOSPrimaryAccount(FALSE);

  std::weak_ptr<Session> weak = weak_from_this();
  const HRESULT hr = ::CreateCoreWebView2EnvironmentWithOptions(
      nullptr, folder.c_str(), options.Get(),
      Callback<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler>([weak, slot](HRESULT result, ICoreWebView2Environment* environment) -> HRESULT {
        auto self = weak.lock();
        if (FAILED(result) || !environment) {
          if (self) self->ShowNotice(RuntimeNotice(self->context_.config.locale));
          return S_OK;
        }
        Remember(slot, environment);
        if (self && !self->closed_) self->CreateController(environment);
        return S_OK;
      }).Get());
  if (FAILED(hr)) ShowNotice(RuntimeNotice(context_.config.locale));
}

void Session::CreateController(ICoreWebView2Environment* environment) {
  ComPtr<ICoreWebView2Environment> held(environment);
  std::weak_ptr<Session> weak = weak_from_this();
  const HRESULT hr = environment->CreateCoreWebView2Controller(
      window_,
      Callback<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler>([weak, held](HRESULT result, ICoreWebView2Controller* controller) -> HRESULT {
        auto self = weak.lock();
        if (!self || self->closed_) {
          if (controller) controller->Close();
          return S_OK;
        }
        if (FAILED(result) || !controller) {
          self->ShowNotice(RuntimeNotice(self->context_.config.locale));
          return S_OK;
        }
        self->OnControllerReady(held.Get(), controller);
        return S_OK;
      }).Get());
  if (FAILED(hr)) ShowNotice(RuntimeNotice(context_.config.locale));
}

void Session::OnControllerReady(ICoreWebView2Environment* environment, ICoreWebView2Controller* controller) {
  environment_ = environment;
  controller_ = controller;
  if (FAILED(controller_->get_CoreWebView2(&view_)) || !view_) return;

  ComPtr<ICoreWebView2Controller2> controller2;
  if (SUCCEEDED(controller_.As(&controller2))) {
    COREWEBVIEW2_COLOR color{255, GetRValue(background_), GetGValue(background_), GetBValue(background_)};
    controller2->put_DefaultBackgroundColor(color);
  }
  ApplyBounds();
  ConfigureSettings();
  AttachEvents();
  view_->Navigate(ResourceServer::kAppUrl);
}

void Session::ConfigureSettings() {
  ComPtr<ICoreWebView2Settings> settings;
  if (FAILED(view_->get_Settings(&settings)) || !settings) return;
  settings->put_IsScriptEnabled(TRUE);
  settings->put_IsWebMessageEnabled(TRUE);
  settings->put_AreDefaultScriptDialogsEnabled(FALSE);
  settings->put_IsStatusBarEnabled(FALSE);
  settings->put_AreHostObjectsAllowed(FALSE);
  settings->put_AreDefaultContextMenusEnabled(FALSE);
  settings->put_IsZoomControlEnabled(FALSE);
  settings->put_IsBuiltInErrorPageEnabled(FALSE);
  settings->put_AreDevToolsEnabled(FALSE);

  ComPtr<ICoreWebView2Settings3> settings3;
  if (SUCCEEDED(settings.As(&settings3))) settings3->put_AreBrowserAcceleratorKeysEnabled(FALSE);
  ComPtr<ICoreWebView2Settings4> settings4;
  if (SUCCEEDED(settings.As(&settings4))) {
    settings4->put_IsPasswordAutosaveEnabled(FALSE);
    settings4->put_IsGeneralAutofillEnabled(FALSE);
  }
  ComPtr<ICoreWebView2Settings5> settings5;
  if (SUCCEEDED(settings.As(&settings5))) settings5->put_IsPinchZoomEnabled(FALSE);
  ComPtr<ICoreWebView2Settings6> settings6;
  if (SUCCEEDED(settings.As(&settings6))) settings6->put_IsSwipeNavigationEnabled(FALSE);
  ComPtr<ICoreWebView2Settings8> settings8;
  if (SUCCEEDED(settings.As(&settings8))) settings8->put_IsReputationCheckingRequired(FALSE);
}

void Session::AttachEvents() {
  std::weak_ptr<Session> weak = weak_from_this();
  EventRegistrationToken token{};

  ComPtr<ICoreWebView2_22> view22;
  if (SUCCEEDED(view_.As(&view22))) {
    view22->AddWebResourceRequestedFilterWithRequestSourceKinds(L"*", COREWEBVIEW2_WEB_RESOURCE_CONTEXT_ALL, COREWEBVIEW2_WEB_RESOURCE_REQUEST_SOURCE_KINDS_ALL);
  } else {
    view_->AddWebResourceRequestedFilter(L"*", COREWEBVIEW2_WEB_RESOURCE_CONTEXT_ALL);
  }

  view_->add_WebResourceRequested(
      Callback<ICoreWebView2WebResourceRequestedEventHandler>([weak](ICoreWebView2*, ICoreWebView2WebResourceRequestedEventArgs* args) -> HRESULT {
        auto self = weak.lock();
        if (self && !self->closed_ && self->environment_) self->server_->Handle(self->environment_.Get(), args);
        return S_OK;
      }).Get(),
      &token);

  view_->add_NavigationStarting(
      Callback<ICoreWebView2NavigationStartingEventHandler>([](ICoreWebView2*, ICoreWebView2NavigationStartingEventArgs* args) -> HRESULT {
        LPWSTR raw = nullptr;
        args->get_Uri(&raw);
        if (Text::Take(raw) != ResourceServer::kAppUrl) args->put_Cancel(TRUE);
        return S_OK;
      }).Get(),
      &token);

  view_->add_FrameNavigationStarting(
      Callback<ICoreWebView2NavigationStartingEventHandler>([weak](ICoreWebView2*, ICoreWebView2NavigationStartingEventArgs* args) -> HRESULT {
        auto self = weak.lock();
        LPWSTR raw = nullptr;
        args->get_Uri(&raw);
        const std::wstring uri = Text::Take(raw);
        if (!self || !self->server_->AllowsFrame(uri)) args->put_Cancel(TRUE);
        return S_OK;
      }).Get(),
      &token);

  view_->add_NewWindowRequested(
      Callback<ICoreWebView2NewWindowRequestedEventHandler>([](ICoreWebView2*, ICoreWebView2NewWindowRequestedEventArgs* args) -> HRESULT {
        args->put_Handled(TRUE);
        return S_OK;
      }).Get(),
      &token);

  view_->add_PermissionRequested(
      Callback<ICoreWebView2PermissionRequestedEventHandler>([](ICoreWebView2*, ICoreWebView2PermissionRequestedEventArgs* args) -> HRESULT {
        args->put_State(COREWEBVIEW2_PERMISSION_STATE_DENY);
        return S_OK;
      }).Get(),
      &token);

  view_->add_WebMessageReceived(
      Callback<ICoreWebView2WebMessageReceivedEventHandler>([weak](ICoreWebView2*, ICoreWebView2WebMessageReceivedEventArgs* args) -> HRESULT {
        auto self = weak.lock();
        if (!self) return S_OK;
        LPWSTR raw = nullptr;
        if (FAILED(args->get_Source(&raw))) return S_OK;
        if (Text::Take(raw) != ResourceServer::kAppUrl) return S_OK;
        raw = nullptr;
        if (FAILED(args->TryGetWebMessageAsString(&raw))) return S_OK;
        self->OnCommand(Text::Take(raw));
        return S_OK;
      }).Get(),
      &token);

  view_->add_ProcessFailed(
      Callback<ICoreWebView2ProcessFailedEventHandler>([weak](ICoreWebView2*, ICoreWebView2ProcessFailedEventArgs* args) -> HRESULT {
        auto self = weak.lock();
        if (!self || self->closed_) return S_OK;
        COREWEBVIEW2_PROCESS_FAILED_KIND kind{};
        args->get_ProcessFailedKind(&kind);
        if (kind != COREWEBVIEW2_PROCESS_FAILED_KIND_BROWSER_PROCESS_EXITED && kind != COREWEBVIEW2_PROCESS_FAILED_KIND_RENDER_PROCESS_EXITED && kind != COREWEBVIEW2_PROCESS_FAILED_KIND_RENDER_PROCESS_UNRESPONSIVE) return S_OK;
        if (kind == COREWEBVIEW2_PROCESS_FAILED_KIND_BROWSER_PROCESS_EXITED) {
          Remember(0, nullptr);
          Remember(1, nullptr);
        }
        if (self->restarts_++ < kMaximumRestarts) ::PostMessageW(self->window_, kRestartMessage, 0, 0);
        return S_OK;
      }).Get(),
      &token);

  ComPtr<ICoreWebView2_4> view4;
  if (SUCCEEDED(view_.As(&view4))) {
    view4->add_DownloadStarting(
        Callback<ICoreWebView2DownloadStartingEventHandler>([](ICoreWebView2*, ICoreWebView2DownloadStartingEventArgs* args) -> HRESULT {
          args->put_Cancel(TRUE);
          args->put_Handled(TRUE);
          return S_OK;
        }).Get(),
        &token);
  }

  ComPtr<ICoreWebView2_5> view5;
  if (SUCCEEDED(view_.As(&view5))) {
    view5->add_ClientCertificateRequested(
        Callback<ICoreWebView2ClientCertificateRequestedEventHandler>([](ICoreWebView2*, ICoreWebView2ClientCertificateRequestedEventArgs* args) -> HRESULT {
          args->put_Cancel(TRUE);
          return S_OK;
        }).Get(),
        &token);
  }

  ComPtr<ICoreWebView2_10> view10;
  if (SUCCEEDED(view_.As(&view10))) {
    view10->add_BasicAuthenticationRequested(
        Callback<ICoreWebView2BasicAuthenticationRequestedEventHandler>([](ICoreWebView2*, ICoreWebView2BasicAuthenticationRequestedEventArgs* args) -> HRESULT {
          args->put_Cancel(TRUE);
          return S_OK;
        }).Get(),
        &token);
  }

  ComPtr<ICoreWebView2_18> view18;
  if (SUCCEEDED(view_.As(&view18))) {
    view18->add_LaunchingExternalUriScheme(
        Callback<ICoreWebView2LaunchingExternalUriSchemeEventHandler>([](ICoreWebView2*, ICoreWebView2LaunchingExternalUriSchemeEventArgs* args) -> HRESULT {
          args->put_Cancel(TRUE);
          return S_OK;
        }).Get(),
        &token);
  }

  controller_->add_AcceleratorKeyPressed(
      Callback<ICoreWebView2AcceleratorKeyPressedEventHandler>([weak](ICoreWebView2Controller*, ICoreWebView2AcceleratorKeyPressedEventArgs* args) -> HRESULT {
        auto self = weak.lock();
        if (!self) return S_OK;
        COREWEBVIEW2_KEY_EVENT_KIND kind{};
        args->get_KeyEventKind(&kind);
        if (kind != COREWEBVIEW2_KEY_EVENT_KIND_KEY_DOWN && kind != COREWEBVIEW2_KEY_EVENT_KIND_SYSTEM_KEY_DOWN) return S_OK;
        UINT key = 0;
        args->get_VirtualKey(&key);
        const bool system = kind == COREWEBVIEW2_KEY_EVENT_KIND_SYSTEM_KEY_DOWN;
        const bool functionKey = key >= VK_F1 && key <= VK_F24 && key != VK_F3;
        if ((system || functionKey) && self->Forward(key, system)) args->put_Handled(TRUE);
        return S_OK;
      }).Get(),
      &token);

  controller_->add_MoveFocusRequested(
      Callback<ICoreWebView2MoveFocusRequestedEventHandler>([weak](ICoreWebView2Controller*, ICoreWebView2MoveFocusRequestedEventArgs* args) -> HRESULT {
        auto self = weak.lock();
        if (self && self->Forward(VK_TAB, false)) args->put_Handled(TRUE);
        return S_OK;
      }).Get(),
      &token);
}

bool Session::Forward(UINT virtualKey, bool system) {
  if (!frame_) return false;
  MSG message{};
  message.hwnd = window_;
  message.message = system ? WM_SYSKEYDOWN : WM_KEYDOWN;
  message.wParam = virtualKey;
  return frame_->TranslateAccelerator(&message) == S_OK;
}

void Session::OnCommand(const std::wstring& command) {
  const bool restricted = context_.trust.level == Trust::Level::Restricted;

  if (command == L"allow-relative") {
    if (!restricted) context_.allow.relative = true;
    Post(L"{\"type\":\"ack\",\"command\":\"allow-relative\"}");
    return;
  }

  if (command == L"allow-scripts") {
    if (!restricted) {
      context_.allow.relative = true;
      context_.allow.scripts = true;
      Context::GrantScriptConsent(context_.path);
    }
    Post(L"{\"type\":\"ack\",\"command\":\"allow-scripts\"}");
    return;
  }

  if (command == L"allow-network") {
    if (restricted || context_.trust.marked || context_.allow.network) return;
    context_.allow.relative = true;
    context_.allow.scripts = true;
    context_.allow.network = true;
    context_.resume = true;
    ::PostMessageW(window_, kRestartMessage, 0, 0);
    return;
  }

  if (command == L"convert") {
    if (conversion_ || context_.sniff.demuxer.empty() || !context_.mismatch.empty()) return;
    const bool audio = context_.type.kind == Kind::Audio;
    conversion_ = Conversion::Start(window_, kConvertedMessage, context_.path, context_.sniff.demuxer, audio);
    if (!conversion_) OnConverted(false);
  }
}

void Session::OnConverted(bool success) {
  if (!success || !conversion_) {
    Post(L"{\"type\":\"convert-failed\"}");
    return;
  }
  context_.convertedPath = conversion_->Output();
  const std::wstring name = context_.convertedPath.substr(context_.convertedPath.find_last_of(L'\\') + 1);
  context_.convertedRoute = L"converted/" + name;
  Post(L"{\"type\":\"converted\",\"url\":" + Text::Json(L"https://" + context_.docHost + L"/__flect/" + context_.convertedRoute) + L"}");
}

void Session::Post(const std::wstring& json) {
  if (view_) view_->PostWebMessageAsJson(json.c_str());
}

