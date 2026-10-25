// Writes a readable report to the log when the game dies: uncaught C++ exceptions,
// abort(), CRT invalid-parameter failures and hardware faults, with a symbolized
// host stack trace. Players attach the log to bug reports.

#include "crash_handler.h"

#ifdef _WIN32

#include <csignal>
#include <cstdio>
#include <cstdlib>
#include <exception>
#include <mutex>
#include <string>
#include <typeinfo>

#include <windows.h>
#include <dbghelp.h>

#include <rex/logging.h>

namespace fifawc10 {
namespace {

std::once_flag g_sym_once;
std::mutex g_report_mutex;

void InitSymbols() {
  std::call_once(g_sym_once, [] {
    SymSetOptions(SYMOPT_UNDNAME | SYMOPT_DEFERRED_LOADS | SYMOPT_LOAD_LINES);
    SymInitialize(GetCurrentProcess(), nullptr, TRUE);
  });
}

void LogStack(CONTEXT* context) {
  InitSymbols();
  void* frames[48];
  USHORT count = 0;
  if (context) {
    STACKFRAME64 frame{};
    frame.AddrPC.Offset = context->Rip;
    frame.AddrFrame.Offset = context->Rbp;
    frame.AddrStack.Offset = context->Rsp;
    frame.AddrPC.Mode = frame.AddrFrame.Mode = frame.AddrStack.Mode = AddrModeFlat;
    CONTEXT copy = *context;
    while (count < 48 && StackWalk64(IMAGE_FILE_MACHINE_AMD64, GetCurrentProcess(),
                                     GetCurrentThread(), &frame, &copy, nullptr,
                                     SymFunctionTableAccess64, SymGetModuleBase64, nullptr)) {
      if (!frame.AddrPC.Offset) break;
      frames[count++] = reinterpret_cast<void*>(frame.AddrPC.Offset);
    }
  } else {
    count = CaptureStackBackTrace(1, 48, frames, nullptr);
  }

  alignas(SYMBOL_INFO) char buffer[sizeof(SYMBOL_INFO) + 256];
  auto* symbol = reinterpret_cast<SYMBOL_INFO*>(buffer);
  for (USHORT i = 0; i < count; ++i) {
    DWORD64 address = reinterpret_cast<DWORD64>(frames[i]);
    symbol->SizeOfStruct = sizeof(SYMBOL_INFO);
    symbol->MaxNameLen = 255;
    DWORD64 displacement = 0;
    char module_name[MAX_PATH] = "?";
    HMODULE module = nullptr;
    if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                               GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           reinterpret_cast<LPCSTR>(address), &module)) {
      GetModuleFileNameA(module, module_name, MAX_PATH);
    }
    const char* short_module = strrchr(module_name, '\\');
    short_module = short_module ? short_module + 1 : module_name;
    if (SymFromAddr(GetCurrentProcess(), address, &displacement, symbol)) {
      IMAGEHLP_LINE64 line{};
      line.SizeOfStruct = sizeof(line);
      DWORD line_disp = 0;
      if (SymGetLineFromAddr64(GetCurrentProcess(), address, &line_disp, &line)) {
        REXLOG_CRITICAL("  #{:02} {}!{}+0x{:X} ({}:{})", i, short_module, symbol->Name,
                        displacement, line.FileName, line.LineNumber);
      } else {
        REXLOG_CRITICAL("  #{:02} {}!{}+0x{:X}", i, short_module, symbol->Name, displacement);
      }
    } else {
      REXLOG_CRITICAL("  #{:02} {}+0x{:X}", i, short_module,
                      address - reinterpret_cast<DWORD64>(module));
    }
  }
}

void Report(const std::string& reason, CONTEXT* context) {
  std::lock_guard lock(g_report_mutex);
  REXLOG_CRITICAL("==================== CRASH ====================");
  REXLOG_CRITICAL("{}", reason);
  LogStack(context);
  REXLOG_CRITICAL("===============================================");
  rex::FlushLogging();
}

void OnTerminate() {
  std::string reason = "Uncaught C++ exception";
  if (auto current = std::current_exception()) {
    try {
      std::rethrow_exception(current);
    } catch (const std::exception& e) {
      reason += fmt::format(" ({}): {}", typeid(e).name(), e.what());
    } catch (...) {
      reason += " (unknown type)";
    }
  } else {
    reason = "std::terminate called without an active exception";
  }
  Report(reason, nullptr);
  _exit(3);
}

void OnAbort(int) {
  Report("abort() called", nullptr);
  _exit(3);
}

void OnInvalidParameter(const wchar_t* expression, const wchar_t* function, const wchar_t* file,
                        unsigned int line, uintptr_t) {
  Report("CRT invalid parameter", nullptr);
  _exit(3);
}

LONG WINAPI OnUnhandled(EXCEPTION_POINTERS* info) {
  auto* record = info->ExceptionRecord;
  Report(fmt::format("Unhandled exception 0x{:08X} at 0x{:X}", record->ExceptionCode,
                     reinterpret_cast<uintptr_t>(record->ExceptionAddress)),
         info->ContextRecord);
  return EXCEPTION_CONTINUE_SEARCH;
}

}  // namespace

void LogStackFrom(void* context) {
  std::lock_guard lock(g_report_mutex);
  LogStack(static_cast<CONTEXT*>(context));
}

void InstallCrashHandler() {
  std::set_terminate(OnTerminate);
  signal(SIGABRT, OnAbort);
  _set_invalid_parameter_handler(OnInvalidParameter);
  _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);
  SetUnhandledExceptionFilter(OnUnhandled);
}

}  // namespace fifawc10

#else

namespace fifawc10 {
void InstallCrashHandler() {}
void LogStackFrom(void*) {}
}  // namespace fifawc10

#endif
