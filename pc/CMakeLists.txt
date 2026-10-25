# fifawc10 - ReXGlue Recompiled Project
#
# SDK boilerplate lives in generated/rexglue.cmake.

cmake_minimum_required(VERSION 3.25)
# LLVM's resource compiler for the exe's icon and version info.
if(NOT CMAKE_RC_COMPILER)
    find_program(LLVM_RC llvm-rc HINTS "$ENV{ProgramFiles}/LLVM/bin")
    if(LLVM_RC)
        set(CMAKE_RC_COMPILER "${LLVM_RC}")
    endif()
endif()
project(fifawc10 LANGUAGES CXX RC)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(generated/rexglue.cmake)

# Sources
set(FIFAWC10_SOURCES
    src/main.cpp
    src/crash_handler.cpp
    src/data_folder.cpp
    src/debug_watch.cpp
    src/fps_overlay.cpp
    src/perf_log.cpp
    src/open_launcher.cpp
)

# Icon and version (VERSION at the repository root).
file(READ "${CMAKE_CURRENT_SOURCE_DIR}/../VERSION" FIFAWC10_VERSION)
string(STRIP "${FIFAWC10_VERSION}" FIFAWC10_VERSION)
string(REPLACE "." ";" _v "${FIFAWC10_VERSION}")
list(GET _v 0 FIFAWC10_VERSION_MAJOR)
list(GET _v 1 FIFAWC10_VERSION_MINOR)
list(GET _v 2 FIFAWC10_VERSION_PATCH)
set(FIFAWC10_ICON "${CMAKE_CURRENT_SOURCE_DIR}/../launcher/icon.ico")
configure_file(res/fifawc10.rc.in "${CMAKE_CURRENT_BINARY_DIR}/fifawc10.rc" @ONLY)
list(APPEND FIFAWC10_SOURCES "${CMAKE_CURRENT_BINARY_DIR}/fifawc10.rc")

if(WIN32)
    add_executable(fifawc10 WIN32 ${FIFAWC10_SOURCES})
else()
    add_executable(fifawc10 ${FIFAWC10_SOURCES})
endif()

rexglue_setup_target(fifawc10 GPU_PLUGINS xenos)

if(WIN32)
    target_link_libraries(fifawc10 PRIVATE dbghelp)
endif()


# Hooks for functions in the game module (fifadllzf.xex) have to live in its DLL.
if(TARGET fifawc10_fifadllzf)
    target_sources(fifawc10_fifadllzf PRIVATE
        fixes/boot_fixes.cpp
        fixes/lua_diagnostics.cpp)
endif()

