// 2010 FIFA World Cup South Africa: Recompiled - the Windows app.

#pragma once

#include <filesystem>

#include <rex/cvar.h>
#include <rex/filesystem.h>
#include <rex/filesystem/vfs.h>
#include <rex/rex_app.h>
#include <rex/runtime.h>
#include <rex/ui/window.h>

#include "crash_handler.h"
#include "data_folder.h"
#include "debug_watch.h"
#include "open_launcher.h"
#include "perf_log.h"

class Fifawc10App : public rex::ReXApp {
 public:
  using rex::ReXApp::ReXApp;

  static std::unique_ptr<rex::ui::WindowedApp> Create(rex::ui::WindowedAppContext& ctx) {
    return std::unique_ptr<Fifawc10App>(new Fifawc10App(ctx, "fifawc10", PPCImageConfig));
  }

  void OnConfigurePaths(rex::PathConfig& paths) override {
    // Everything we write goes to one data folder (data_folder.h): the config the
    // launcher writes, saves and the shader cache. Paths given on the command line win.
    auto data = fifawc10::DataFolder();
    std::error_code ec;
    // The data folder is also the working folder, so relative paths (logs) land there.
    std::filesystem::current_path(data, ec);
    paths.config_path = data / "fifawc10.toml";
    // The SDK reads game_data_root before it loads the config file, so a game path
    // that only lives in the config would be missed. Load it early.
    if (paths.game_data_root.empty() && std::filesystem::exists(paths.config_path, ec)) {
      rex::cvar::LoadConfig(paths.config_path);
      paths.game_data_root = rex::to_path(REXCVAR_GET(game_data_root));
    }
    if (REXCVAR_GET(user_data_root).empty()) paths.user_data_root = data / "saves";
    if (REXCVAR_GET(cache_root).empty()) paths.cache_root = data / "cache";
    // No game copy yet: the launcher sets one up.
    fifawc10::OpenLauncherIfNoGame(paths.game_data_root);
  }

  // Logs next to saves and settings, not next to the exe (which may be in Program Files).
  void OnConfigureLogging(rex::LogConfig& config) override {
    config.log_dir = fifawc10::DataFolder() / "logs";
  }

  void OnPostInitLogging() override { fifawc10::InstallCrashHandler(); }

  void OnPreSetup(rex::RuntimeConfig& config) override {
    // The Direct3D 12 Xenos renderer, unless the config file picks another plugin.
    if (config.gpu_plugin.empty()) config.gpu_plugin = "xenos";
  }

  void OnPostSetup() override {
    if (auto* w = window()) w->SetTitle("2010 FIFA World Cup South Africa: Recompiled");
    // default.xex loads the game module by a bare name ("fifadllzf.xex"). The console
    // resolves that against the game disc; here we point it at game:\ ourselves.
    if (auto* fs = runtime()->file_system()) {
      fs->RegisterSymbolicLink("fifadllzf.xex", "game:\\fifadllzf.xex");
    }
    fifawc10::StartDebugWatch();
    fifawc10::StartPerfLog();
  }
};
