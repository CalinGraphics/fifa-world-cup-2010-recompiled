#include "data_folder.h"

#include <cstdlib>
#include <system_error>

#include <rex/filesystem.h>

namespace fifawc10 {

std::filesystem::path DataFolder() {
  static const std::filesystem::path folder = [] {
    std::error_code ec;
    auto exe_dir = rex::filesystem::GetExecutableFolder();
    std::filesystem::path path;
    if (std::filesystem::exists(exe_dir / "portable.txt", ec)) {
      path = exe_dir / "userdata";
    } else if (const wchar_t* local = _wgetenv(L"LOCALAPPDATA"); local && *local) {
      path = std::filesystem::path(local) / "FIFA World Cup 2010 Recompiled";
    } else {
      path = exe_dir / "userdata";
    }
    std::filesystem::create_directories(path / "cache", ec);
    std::filesystem::create_directories(path / "saves", ec);
    return path;
  }();
  return folder;
}

}  // namespace fifawc10
