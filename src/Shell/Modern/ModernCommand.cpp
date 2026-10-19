#include "Bridge.h"

#include <memory>
#include <wrl/implements.h>
#pragma warning(push)
#pragma warning(disable: 4324)
#include <wrl/module.h>
#pragma warning(pop)

using namespace Microsoft::WRL;
using Classic = RuntimeClassFlags<ClassicCom>;

struct MenuContext {
  SelectionInfo selection;
  std::vector<MenuNode> nodes;
};

class Commands final : public RuntimeClass<Classic, IEnumExplorerCommand> {
 public:
  std::vector<ComPtr<IExplorerCommand>> values;
  size_t position = 0;
  IFACEMETHODIMP Next(ULONG count, IExplorerCommand** output, ULONG* fetched) override {
    if (fetched) *fetched = 0;
    if (!output || (!fetched && count != 1)) return E_POINTER;
    ULONG copied = 0;
    while (copied < count && position < values.size()) {
      values[position++].CopyTo(&output[copied++]);
    }
    if (fetched) *fetched = copied;
    return copied == count ? S_OK : S_FALSE;
  }
  IFACEMETHODIMP Skip(ULONG count) override {
    const size_t remaining = values.size() - position;
    position += (std::min)(remaining, static_cast<size_t>(count));
    return remaining >= count ? S_OK : S_FALSE;
  }
  IFACEMETHODIMP Reset() override { position = 0; return S_OK; }
  IFACEMETHODIMP Clone(IEnumExplorerCommand** output) override {
    if (!output) return E_POINTER;
    auto copy = Make<Commands>();
    if (!copy) return E_OUTOFMEMORY;
    copy->values = values;
    copy->position = position;
    return copy.CopyTo(output);
  }
};

class Command final : public RuntimeClass<Classic, IExplorerCommand, IObjectWithSite> {
 public:
  Command() = default;
  Command(std::shared_ptr<MenuContext> context, size_t index)
      : context_(std::move(context)), index_(index), root_(false) {}

  IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* result) override {
    if (!result) return E_POINTER;
    return SHStrDupW(root_ ? BRAND_NAME : context_->nodes[index_].label.c_str(), result);
  }
  IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* result) override {
    if (!result) return E_POINTER;
    *result = nullptr;
    if (!root_) return E_NOTIMPL;
    return SHStrDupW((Bridge::AppDirectory() + L"\\" + BRAND_APP_EXE + L",0").c_str(), result);
  }
  IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* result) override {
    if (!result) return E_POINTER;
    *result = nullptr;
    return E_NOTIMPL;
  }
  IFACEMETHODIMP GetCanonicalName(GUID* result) override {
    if (!result) return E_POINTER;
    CLSIDFromString(Bridge::kClsid, result);
    if (!root_) {
      unsigned long hash = 2166136261u;
      for (wchar_t c : context_->nodes[index_].id) hash = (hash ^ c) * 16777619u;
      result->Data1 ^= hash;
    }
    return S_OK;
  }
  IFACEMETHODIMP GetState(IShellItemArray* items, BOOL slow, EXPCMDSTATE* state) override {
    if (!state) return E_POINTER;
    if (!root_) { *state = ECS_ENABLED; return S_OK; }
    *state = ECS_HIDDEN;
    if (!items) { context_.reset(); return S_OK; }
    if (!slow) return E_PENDING;
    try {
      auto context = std::make_shared<MenuContext>();
      context->selection = Bridge::Selection(items);
      context->nodes = MenuBuilder::Build(context->selection, Config::Get());
      *state = context->nodes.empty() ? ECS_HIDDEN : ECS_ENABLED;
      context_ = std::move(context);
      return S_OK;
    } catch (const std::bad_alloc&) { return E_OUTOFMEMORY; }
      catch (...) { return E_FAIL; }
  }
  IFACEMETHODIMP GetFlags(EXPCMDFLAGS* result) override {
    if (!result) return E_POINTER;
    *result = root_ ? ECF_HASSUBCOMMANDS : ECF_DEFAULT;
    return S_OK;
  }
  IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** output) override {
    if (!output) return E_POINTER;
    *output = nullptr;
    if (!root_ || !context_) return E_NOTIMPL;
    try {
      auto commands = Make<Commands>();
      if (!commands) return E_OUTOFMEMORY;
      for (size_t i = 0; i < context_->nodes.size(); ++i) {
        auto command = Make<Command>(context_, i);
        if (!command) return E_OUTOFMEMORY;
        commands->values.push_back(command);
      }
      return commands.CopyTo(output);
    } catch (const std::bad_alloc&) { return E_OUTOFMEMORY; }
      catch (...) { return E_FAIL; }
  }
  IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override {
    if (root_) return E_NOTIMPL;
    try {
      auto selection = items ? Bridge::Selection(items) : context_->selection;
      if (selection.Empty()) return E_INVALIDARG;
      return Bridge::StartMenu(selection.paths, context_->nodes[index_].id);
    } catch (const std::bad_alloc&) { return E_OUTOFMEMORY; }
      catch (...) { return E_FAIL; }
  }
  IFACEMETHODIMP SetSite(IUnknown* site) override { site_ = site; return S_OK; }
  IFACEMETHODIMP GetSite(REFIID iid, void** output) override {
    if (!output) return E_POINTER;
    *output = nullptr;
    return site_ ? site_->QueryInterface(iid, output) : E_FAIL;
  }
 private:
  ComPtr<IUnknown> site_;
  std::shared_ptr<MenuContext> context_;
  size_t index_ = 0;
  bool root_ = true;
};

class Factory final : public RuntimeClass<Classic, IClassFactory> {
 public:
  IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID iid, void** output) override {
    if (!output) return E_POINTER;
    *output = nullptr;
    if (outer) return CLASS_E_NOAGGREGATION;
    auto command = Make<Command>();
    return command ? command.CopyTo(iid, output) : E_OUTOFMEMORY;
  }
  IFACEMETHODIMP LockServer(BOOL lock) override {
    auto& module = Microsoft::WRL::Module<InProc>::GetModule();
    if (lock) module.IncrementObjectCount(); else module.DecrementObjectCount();
    return S_OK;
  }
};

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
  if (reason == DLL_PROCESS_ATTACH) DisableThreadLibraryCalls(instance);
  return TRUE;
}
STDAPI DllGetClassObject(REFCLSID clsid, REFIID iid, void** output) {
  if (!output) return E_POINTER;
  *output = nullptr;
  CLSID ours{};
  CLSIDFromString(Bridge::kClsid, &ours);
  if (clsid != ours) return CLASS_E_CLASSNOTAVAILABLE;
  (void)Microsoft::WRL::Module<InProc>::GetModule();
  auto factory = Make<Factory>();
  return factory ? factory.CopyTo(iid, output) : E_OUTOFMEMORY;
}
STDAPI DllCanUnloadNow() {
  return Microsoft::WRL::Module<InProc>::GetModule().GetObjectCount() ? S_FALSE : S_OK;
}
