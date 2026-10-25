// Developer tool: log every guest write to a watched word, with the host stack
// (recompiled functions are named after their guest address, so the stack shows the
// game's own call chain).
//
// Enabled with the environment variable FIFAWC10_WATCH:
//   FIFAWC10_WATCH=83A4648C            watch the word at that guest address
//   FIFAWC10_WATCH=*83A4648C+64        wait until the pointer at 83A4648C is set,
//                                      then watch the word at pointer + 0x64
// The page holding the word is made read-only; a vectored handler logs writes that hit
// the word, lets the write through with a single step, then protects the page again.

#include "debug_watch.h"

#ifdef _WIN32

#include <atomic>
#include <cstdint>
#include <cstdlib>
#include <string>
#include <thread>

#include <windows.h>

#include <rex/logging.h>

#include "crash_handler.h"

namespace fifawc10 {
namespace {

constexpr uint64_t kGuestVirtualBase = 0x100000000ull;  // SDK guest arena (see run log)

std::atomic<uintptr_t> g_page{0};
std::atomic<uintptr_t> g_word{0};
thread_local bool t_stepping = false;

void Protect(bool on) {
  DWORD old;
  VirtualProtect(reinterpret_cast<void*>(g_page.load()), 0x1000, on ? PAGE_READONLY : PAGE_READWRITE,
                 &old);
}

LONG CALLBACK Handler(EXCEPTION_POINTERS* info) {
  auto* rec = info->ExceptionRecord;
  if (rec->ExceptionCode == EXCEPTION_SINGLE_STEP && t_stepping) {
    t_stepping = false;
    Protect(true);
    return EXCEPTION_CONTINUE_EXECUTION;
  }
  if (rec->ExceptionCode != EXCEPTION_ACCESS_VIOLATION || rec->NumberParameters < 2 ||
      rec->ExceptionInformation[0] != 1) {
    return EXCEPTION_CONTINUE_SEARCH;
  }
  uintptr_t addr = rec->ExceptionInformation[1];
  uintptr_t page = g_page.load();
  if (!page || addr < page || addr >= page + 0x1000) return EXCEPTION_CONTINUE_SEARCH;
  uintptr_t word = g_word.load();
  if (std::getenv("FIFAWC10_WATCH_RANGE")) {
    // Whole-object mode: one line per write with its offset from the watched word.
    long long off = (long long)addr - (long long)word;
    if (off >= -0x64 && off < 0x100) {
      REXLOG_WARN("WATCH: write at offset {:+X} from {:X}", off, uint32_t(word - kGuestVirtualBase));
      LogStackFrom(info->ContextRecord);
    }
  } else if (addr >= word && addr < word + 4) {
    REXLOG_WARN("WATCH: write to guest {:08X} (host {:X})", uint32_t(word - kGuestVirtualBase),
                addr);
    LogStackFrom(info->ContextRecord);
  }
  Protect(false);
  t_stepping = true;
  info->ContextRecord->EFlags |= 0x100;  // trap flag: re-protect after this instruction
  return EXCEPTION_CONTINUE_EXECUTION;
}

uint32_t LoadGuest32(uint32_t guest) {
  auto* p = reinterpret_cast<const uint8_t*>(kGuestVirtualBase + guest);
  MEMORY_BASIC_INFORMATION mbi{};
  if (!VirtualQuery(p, &mbi, sizeof(mbi)) || mbi.State != MEM_COMMIT ||
      (mbi.Protect & (PAGE_NOACCESS | PAGE_GUARD))) {
    return 0;  // not mapped yet
  }
  return uint32_t(p[0]) << 24 | uint32_t(p[1]) << 16 | uint32_t(p[2]) << 8 | p[3];
}

void Arm(uint32_t guest_word) {
  uintptr_t host = kGuestVirtualBase + guest_word;
  g_word = host;
  g_page = host & ~uintptr_t(0xFFF);
  Protect(true);
  REXLOG_WARN("WATCH: armed on guest {:08X}", guest_word);
}

}  // namespace

void StartDebugWatch() {
  const char* spec = std::getenv("FIFAWC10_WATCH");
  if (!spec || !*spec) return;
  AddVectoredExceptionHandler(1, Handler);
  std::string s(spec);
  if (s[0] != '*') {
    Arm(uint32_t(std::stoul(s, nullptr, 16)));
    return;
  }
  auto plus = s.find('+');
  uint32_t ptr_addr = uint32_t(std::stoul(s.substr(1, plus - 1), nullptr, 16));
  uint32_t offset = plus == std::string::npos ? 0 : uint32_t(std::stoul(s.substr(plus + 1), nullptr, 16));
  std::thread([ptr_addr, offset] {
    for (int i = 0; i < 2000000; ++i) {
      uint32_t p = LoadGuest32(ptr_addr);
      if (p) {
        Arm(p + offset);
        return;
      }
      std::this_thread::yield();
    }
  }).detach();
}

}  // namespace fifawc10

#else

namespace fifawc10 {
void StartDebugWatch() {}
}  // namespace fifawc10

#endif
