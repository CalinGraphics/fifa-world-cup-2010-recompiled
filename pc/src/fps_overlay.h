// The frame counter in the top-right corner (show_fps; F2 shows or hides it).

#pragma once

#include <chrono>
#include <cstdint>

#include <rex/cvar.h>
#include <rex/ui/imgui_dialog.h>

REXCVAR_DECLARE(bool, show_fps);

namespace fifawc10 {

class FpsOverlay final : public rex::ui::ImGuiDialog {
 public:
  explicit FpsOverlay(rex::ui::ImGuiDrawer* drawer) : rex::ui::ImGuiDialog(drawer) {}

  // It changes with the game's frames, which repaint the window anyway.
  bool WantsContinuousPaint() const override { return false; }

 protected:
  void OnDraw(ImGuiIO& io) override;

 private:
  std::chrono::steady_clock::time_point window_start_{};
  uint64_t window_start_frames_ = 0;
  double fps_ = 0;
};

}  // namespace fifawc10
