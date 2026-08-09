#pragma once

#include "framework.h"

class ClassFactory : public IClassFactory {
 public:
  ClassFactory();

  IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
  IFACEMETHODIMP_(ULONG) AddRef() override;
  IFACEMETHODIMP_(ULONG) Release() override;

  IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override;
  IFACEMETHODIMP LockServer(BOOL lock) override;

 private:
  ~ClassFactory();

  LONG ref_ = 1;
};
