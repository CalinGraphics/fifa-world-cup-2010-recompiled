// Developer tool: logs the game's frame rate once a second (FIFAWC10_PERF_LOG=1).

#include "perf_log.h"

#include <chrono>
#include <cstdlib>
#include <thread>

#include <rex/logging.h>
#include <rex/perf/counter.h>

namespace fifawc10 {

void StartPerfLog() {
  const char* v = std::getenv("FIFAWC10_PERF_LOG");
  if (!v || *v != '1') return;
  std::thread([] {
    for (;;) {
      std::this_thread::sleep_for(std::chrono::seconds(1));
      auto frame_us = rex::perf::GetSnapshotCounter(rex::perf::CounterId::kFrameTimeUs);
      auto draws = rex::perf::GetSnapshotCounter(rex::perf::CounterId::kDrawCalls);
      if (frame_us > 0) {
        REXLOG_INFO("perf: {:.1f} fps ({:.2f} ms/frame, {} draws)", 1e6 / double(frame_us),
                    frame_us / 1000.0, draws);
      }
    }
  }).detach();
}

}  // namespace fifawc10
