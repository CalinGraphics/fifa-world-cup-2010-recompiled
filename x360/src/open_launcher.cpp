// Started without a game copy (double-clicked fifawc10.exe on a fresh install): hand
// over to the launcher, which lets the player pick the disc image and writes the config.

#include "open_launcher.h"

#include <cstdlib>
#include <system_error>

#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>

#include <rex/filesystem.h>

namespace fifawc10 {

void OpenLauncherIfNoGame(const std::filesystem::path& game_data_root) {
  std::error_code ec;
  if (!game_data_root.empty() && std::filesystem::exists(game_data_root, ec)) return;

  auto exe_dir = rex::filesystem::GetExecutableFolder();
  auto launcher = exe_dir / "Launcher.exe";
  if (std::filesystem::exists(launcher, ec)) {
    ShellExecuteW(nullptr, L"open", launcher.c_str(), nullptr, exe_dir.c_str(), SW_SHOWNORMAL);
  } else {
    MessageBoxW(nullptr,
                L"No game copy is set up yet.\n\nStart Launcher.exe and choose your "
                L"Xbox 360 disc image of 2010 FIFA World Cup South Africa.",
                L"2010 FIFA World Cup South Africa: Recompiled", MB_OK | MB_ICONINFORMATION);
  }
  std::exit(0);
}

}  // namespace fifawc10
