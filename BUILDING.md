# Building

## Requirements

- Windows 10 or 11 (x64)
- Visual Studio 2022 or newer with the C++ workload (Windows SDK, CMake, Ninja)
- LLVM/clang 18 or newer: `winget install LLVM.LLVM`
- Python 3.9+ with `cryptography` and `capstone` (only for the analysis tools):
  `pip install cryptography capstone`
- .NET 10 SDK (for the launcher)
- Your own **Xbox 360** copy of the game (Europe disc, En/Es/Nl)

All scripts run from PowerShell: `powershell -ExecutionPolicy Bypass -File <script>`.

## 1. ReXGlue SDK

```
tools\setup_sdk.ps1
```

Clones [ReXGlue](https://github.com/rexglue/rexglue-sdk) next to this repository,
pins commit `bd833a2`, applies `patches\rexglue-sdk\*.patch` and builds and installs
the SDK. Pass `-Sdk <path>` to put it somewhere else.

| Patch | What it does |
|---|---|
| 0001 | Builds the USB camera kernel exports the game imports |
| 0002 | Finds a module loaded by bare file name (`fifadllzf.xex`) |
| 0003 | `function_table_base`: moves a module's dispatch table so `default.xex` and `fifadllzf.xex` don't collide |
| 0004 | Precise guest vblank timing (high-resolution timer instead of `Sleep(1)`) |
| 0005 | Lets `game_data_root` be an Xbox 360 disc image (`.iso`) |

## 2. Generate and build the game

```
tools\build.ps1 -Game "<path to your .iso or game folder>"
```

This extracts `default.xex` and `fifadllzf.xex` to `..\game-x360`, runs the ReXGlue
code generator (`x360\fifawc10_manifest.toml` plus `x360\config\*.toml`) and builds
`x360\out\build\win-amd64-release\fifawc10.exe`. Use `-Config relwithdebinfo` for a
build with debug info.

`x360\generated\` is created from the game binary. It stays on your PC and is git-ignored.

## 3. Package

```
tools\package.ps1
```

Builds the launcher and puts the game, launcher, Visual C++ runtime and licenses in
`out\package\FIFA World Cup 2010 Recompiled\`, plus a zip in `out\`.

## Project layout

| Path | What |
|---|---|
| `x360/fifawc10_manifest.toml` | Codegen manifest: `default.xex` and the `fifadllzf.xex` game module |
| `x360/config/` | Analysis overrides: missed function starts, code pointers, setjmp/longjmp |
| `x360/src/` | The Windows app: paths, crash handler, launcher hand-off, developer tools |
| `x360/src/module/` | Hooks compiled into the game module's DLL (boot fix, Lua error log) |
| `launcher/` | WPF launcher: picks and verifies the game copy, settings, starts the game |
| `patches/rexglue-sdk/` | SDK patches |
| `tools/` | Setup, build and packaging scripts; disc and XEX tools; analysis helpers |

## Analysis tools

| Tool | Use |
|---|---|
| `tools/disc.py` | Detect, list and extract Xbox 360 and PS3 disc images |
| `tools/xexinfo.py`, `tools/xeximage.py` | XEX headers; decrypted, decompressed memory image (needs `lzxdec.exe`, built from `tools/lzxdec/` with libmspack) |
| `tools/ppcdis.py` | PowerPC disassembly of an image, PE sections, `.pdata` lookup |
| `tools/autofix_codegen.py` | Re-runs codegen and adds missed function starts until analysis passes |
| `tools/find_code_pointers.py` | Finds functions only reached through pointers (vtables, callbacks) |
| `tools/find_setjmp.py`, `tools/xref.py`, `tools/callers.py` | Locate setjmp/longjmp, cross-references, callers |

## Developer switches

Environment variables read by `fifawc10.exe`:

- `FIFAWC10_PERF_LOG=1` logs the frame rate once a second
- `FIFAWC10_WATCH=<address>` logs every guest write to a word, with the host stack
