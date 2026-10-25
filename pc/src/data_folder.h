#pragma once

#include <filesystem>

namespace fifawc10 {

// Where saves, the shader cache, the config and logs live:
//  - next to the exe in "userdata\" when a portable.txt sits beside the exe;
//  - otherwise %LOCALAPPDATA%\FIFA World Cup 2010 Recompiled.
// Created on first use.
std::filesystem::path DataFolder();

}  // namespace fifawc10
