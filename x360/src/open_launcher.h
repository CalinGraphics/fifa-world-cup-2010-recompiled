#pragma once

#include <filesystem>

namespace fifawc10 {

// Opens Launcher.exe and exits when there is no usable game copy.
void OpenLauncherIfNoGame(const std::filesystem::path& game_data_root);

}  // namespace fifawc10
