// 2010 FIFA World Cup South Africa: Recompiled - the Windows app.

#pragma once

#include <filesystem>

#include <rex/cvar.h>
#include <rex/filesystem.h>
#include <rex/filesystem/vfs.h>
#include <rex/rex_app.h>
#include <rex/runtime.h>
#include <rex/ui/keybinds.h>
#include <rex/ui/overlay/achievement_notification.h>
#include <rex/ui/window.h>

#include "crash_handler.h"
#include "data_folder.h"
#include "debug_watch.h"
#include "fps_overlay.h"
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

  // GPU settings this game needs, unless the config file or command line sets them.
  // Applied once the renderer is loaded (its settings don't exist before) and before
  // the game starts drawing.
  static void ApplyGameGpuDefaults() {
    // The game draws full-screen rectangles (stencil and clear passes) with clipping off.
    // The renderer then guesses their size from the scissor, which can claim the whole
    // render target memory and wipe what the game drew earlier: the screen went black
    // for seconds at a time in Training Ground. Running those few vertex shaders on the
    // CPU gives their real size.
    if (!rex::cvar::HasNonDefaultValue("execute_unclipped_draw_vs_on_cpu")) {
      rex::cvar::SetFlagByName("execute_unclipped_draw_vs_on_cpu", "true");
    }
    // Many of its textures are bound with fetch constants typed "invalid" (type 0), which
    // the console samples like any texture. Without this they come out black, and each one
    // logs a warning (over a thousand a second in places).
    if (!rex::cvar::HasNonDefaultValue("gpu_allow_invalid_fetch_constants")) {
      rex::cvar::SetFlagByName("gpu_allow_invalid_fetch_constants", "true");
    }
  }

  void OnCreateDialogs(rex::ui::ImGuiDrawer* drawer) override {
    if (REXCVAR_GET(show_fps)) fps_overlay_ = std::make_unique<fifawc10::FpsOverlay>(drawer);
    rex::ui::RegisterBind("bind_fps_counter", "F2", "Show or hide the frame counter", [this] {
      if (fps_overlay_) {
        fps_overlay_.reset();
      } else if (auto* d = imgui_drawer()) {
        fps_overlay_ = std::make_unique<fifawc10::FpsOverlay>(d);
      }
    });
  }

  void OnShutdown() override {
    rex::ui::UnregisterBind("bind_fps_counter");
    fps_overlay_.reset();
  }

  // The game has no achievement toasts to show here. Without this dialog nothing keeps
  // the window repainting at the display's full refresh rate between the game's frames.
  std::unique_ptr<rex::ui::AchievementNotificationDialog> CreateAchievementNotificationDialog()
      override {
    return nullptr;
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
    ApplyGameGpuDefaults();
    REXLOG_INFO("fifawc10: unclipped draws on CPU={}, invalid fetch constants={}, frame limit={}",
                rex::cvar::GetFlagByName("execute_unclipped_draw_vs_on_cpu"),
                rex::cvar::GetFlagByName("gpu_allow_invalid_fetch_constants"),
                rex::cvar::GetFlagByName("frame_rate_limit"));
  }

 private:
  std::unique_ptr<fifawc10::FpsOverlay> fps_overlay_;
};
