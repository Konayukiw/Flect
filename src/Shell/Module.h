#pragma once

#include "framework.h"

namespace Module {

void AddRef() noexcept;
void Release() noexcept;
long RefCount() noexcept;

HINSTANCE Instance() noexcept;
void SetInstance(HINSTANCE instance) noexcept;

std::wstring Path();
std::wstring Directory();

const CLSID& Clsid();

}