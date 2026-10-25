#pragma once

namespace fifawc10 {

// Logs a crash report (reason + host stack trace) before the process dies.
void InstallCrashHandler();

// Logs a symbolized host stack starting at a Windows CONTEXT (debug tools).
void LogStackFrom(void* context);

}  // namespace fifawc10
