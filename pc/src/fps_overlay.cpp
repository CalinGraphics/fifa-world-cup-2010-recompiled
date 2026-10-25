#include "fps_overlay.h"

#include <algorithm>
#include <cfloat>
#include <cstdio>

#include <imgui.h>

#include <rex/kernel/xboxkrnl/video.h>

REXCVAR_DEFINE_BOOL(show_fps, false, "Display", "Show the frame counter (F2 toggles it)");

namespace fifawc10 {

void FpsOverlay::OnDraw(ImGuiIO& io) {
  // Frames the game presented, averaged over half a second.
  auto now = std::chrono::steady_clock::now();
  uint64_t frames = rex::kernel::xboxkrnl::VdSwapCount();
  if (window_start_ == std::chrono::steady_clock::time_point{}) {
    window_start_ = now;
    window_start_frames_ = frames;
  }
  double seconds = std::chrono::duration<double>(now - window_start_).count();
  if (seconds >= 0.5) {
    fps_ = double(frames - window_start_frames_) / seconds;
    window_start_ = now;
    window_start_frames_ = frames;
  }

  char text[32];
  std::snprintf(text, sizeof(text), "%d FPS", int(fps_ + 0.5));

  // Scales with the window: readable at 720p and at 4K.
  float size = std::clamp(io.DisplaySize.y / 38.0f, 14.0f, 40.0f);
  float pad = size * 0.35f;
  float margin = size * 0.6f;
  ImFont* font = ImGui::GetFont();
  ImVec2 text_size = font->CalcTextSizeA(size, FLT_MAX, 0.0f, text);
  ImVec2 box_max(io.DisplaySize.x - margin, margin + text_size.y + pad * 2);
  ImVec2 box_min(box_max.x - text_size.x - pad * 2, margin);

  ImDrawList* draw = ImGui::GetForegroundDrawList();
  draw->AddRectFilled(box_min, box_max, IM_COL32(0, 0, 0, 150), size * 0.15f);
  // Green at 55+, amber at 28-54 (the game's own 30 in menus and replays), red below.
  ImU32 color = fps_ >= 55 ? IM_COL32(120, 230, 120, 255)
                           : (fps_ >= 28 ? IM_COL32(240, 200, 90, 255) : IM_COL32(240, 90, 80, 255));
  draw->AddText(font, size, ImVec2(box_min.x + pad, box_min.y + pad), color, text);
}

}  // namespace fifawc10
