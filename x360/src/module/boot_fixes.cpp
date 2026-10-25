// Fixes for the boot sequence. Built into the game module's DLL.
//
// The game keeps its subsystems in a registry object (pointer at 0x83A4648C, getter
// sub_82C501D0). Slot 0x64 holds the game database, a 0x6C6D0-byte object created by
// sub_82C9A4B0 only once a player presses START on the title screen (the boot task,
// sub_82D22390, gets message 0x46 with the pad, then creates it on the next tick).
//
// sub_82CBD7E0 runs every frame from the front-end update (sub_82CA1B50) and loads a
// field at database + 0x6C6B8 before it checks anything. With no database yet that
// read is at guest 0x0006C6B8; the console tolerates it, here it is an access
// violation on the very first frame. Until the database exists the function has
// nothing to update, so it is skipped.

#include <rex/hook.h>
#include <rex/logging.h>
#include <rex/system/kernel_state.h>
#include <rex/types.h>

REX_EXTERN(__imp__sub_82CBD7E0);

namespace {

constexpr uint32_t kRegistryPointer = 0x83A4648C;
constexpr uint32_t kDatabaseSlot = 0x64;

uint32_t Load32(uint32_t address) {
  return *REX_KERNEL_MEMORY()->TranslateVirtual<rex::be<uint32_t>*>(address);
}

bool DatabaseReady() {
  uint32_t registry = Load32(kRegistryPointer);
  return registry && Load32(registry + kDatabaseSlot);
}

}  // namespace

REX_HOOK_RAW(sub_82CBD7E0) {
  if (!DatabaseReady()) {
    static bool logged = false;
    if (!logged) {
      logged = true;
      REXLOG_INFO("boot: database not created yet, skipping its per-frame update");
    }
    return;
  }
  __imp__sub_82CBD7E0(ctx, base);
}
