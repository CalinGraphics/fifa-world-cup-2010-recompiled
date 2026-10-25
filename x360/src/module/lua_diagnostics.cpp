// Logs every Lua error the game raises (fifadllzf.xex embeds Lua 5.1), with its message,
// so script failures don't go unnoticed. Built into the game module's DLL.
//
// sub_829655A8 is luaD_throw(L, errcode): it stores the code in L->errorJmp (0x70) and
// longjmps, or calls the panic handler. The error object is the value at L->top - 1
// (L->top at 0x08, TValue 16 bytes: value at +0, type tag at +8; strings are type 4 and
// keep their characters 16 bytes into the TString).

#include <string>

#include <rex/hook.h>
#include <rex/logging.h>
#include <rex/system/kernel_state.h>
#include <rex/types.h>

REX_EXTERN(__imp__sub_829655A8);

namespace {

uint32_t Load32(uint32_t address) {
  return *REX_KERNEL_MEMORY()->TranslateVirtual<rex::be<uint32_t>*>(address);
}

std::string ErrorMessage(uint32_t L) {
  uint32_t top = Load32(L + 0x08);
  if (!top) return {};
  uint32_t value = top - 16;
  if (Load32(value + 8) != 4) return "(non-string error object)";
  uint32_t tstring = Load32(value);
  uint32_t len = Load32(tstring + 12);
  if (!tstring || len > 4096) return {};
  auto* chars = REX_KERNEL_MEMORY()->TranslateVirtual<const char*>(tstring + 16);
  return std::string(chars, len);
}

int g_logged = 0;

}  // namespace

REX_HOOK_RAW(sub_829655A8) {
  if (g_logged++ < 50) {
    REXLOG_WARN("lua error {}: {}", ctx.r4.u32, ErrorMessage(ctx.r3.u32));
  }
  __imp__sub_829655A8(ctx, base);
}
