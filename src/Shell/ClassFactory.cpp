#include "ClassFactory.h"

#include "ContextMenu.h"
#include "Module.h"

ClassFactory::ClassFactory() { Module::AddRef(); }

ClassFactory::~ClassFactory() { Module::Release(); }

IFACEMETHODIMP ClassFactory::QueryInterface(REFIID riid, void** ppv) {
  if (!ppv) return E_POINTER;
  if (::IsEqualIID(riid, IID_IUnknown) || ::IsEqualIID(riid, IID_IClassFactory)) {
    *ppv = static_cast<IClassFactory*>(this);
    AddRef();
    return S_OK;
  }
  *ppv = nullptr;
  return E_NOINTERFACE;
}

IFACEMETHODIMP_(ULONG) ClassFactory::AddRef() {
  return static_cast<ULONG>(::InterlockedIncrement(&ref_));
}

IFACEMETHODIMP_(ULONG) ClassFactory::Release() {
  const LONG remaining = ::InterlockedDecrement(&ref_);
  if (remaining == 0) delete this;
  return static_cast<ULONG>(remaining);
}

IFACEMETHODIMP ClassFactory::CreateInstance(IUnknown* outer, REFIID riid, void** ppv) {
  if (!ppv) return E_POINTER;
  *ppv = nullptr;
  if (outer) return CLASS_E_NOAGGREGATION;

  auto* handler = new (std::nothrow) ContextMenu();
  if (!handler) return E_OUTOFMEMORY;

  const HRESULT hr = handler->QueryInterface(riid, ppv);
  handler->Release();
  return hr;
}

IFACEMETHODIMP ClassFactory::LockServer(BOOL lock) {
  if (lock) {
    Module::AddRef();
  } else {
    Module::Release();
  }
  return S_OK;
}
