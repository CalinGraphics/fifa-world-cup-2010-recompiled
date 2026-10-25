# fifawc10 - ReXGlue Recompiled Project
#
# SDK boilerplate lives in generated/rexglue.cmake.

cmake_minimum_required(VERSION 3.25)
project(fifawc10 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(generated/rexglue.cmake)

# Sources
set(FIFAWC10_SOURCES
    src/main.cpp
    src/crash_handler.cpp
    src/data_folder.cpp
    src/debug_watch.cpp
    src/perf_log.cpp
    src/open_launcher.cpp
)

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
        src/module/boot_fixes.cpp
        src/module/lua_diagnostics.cpp)
endif()

