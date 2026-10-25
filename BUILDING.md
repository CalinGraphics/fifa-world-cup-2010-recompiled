# Building

## Requirements

- Windows 10 or 11 (x64)
- Visual Studio 2022 or newer with the C++ workload (Windows SDK, CMake, Ninja)
- LLVM/clang 18 or newer: `winget install LLVM.LLVM`
- Python 3.9+ with `cryptography` (and `capstone` for the analysis tools): `pip install cryptography capstone`
- .NET 10 SDK (for the launcher)
- Your own **Xbox 360** copy of the game (Europe disc, En/Es/Nl)

Run the scripts from PowerShell: `powershell -ExecutionPolicy Bypass -File <script> ...`

## 1. ReXGlue SDK

```powershell
tools\build\setup_sdk.ps1
```

Clones [ReXGlue](https://github.com/rexglue/rexglue-sdk) next to this repository, pins commit `bd833a2`,
fixes git symlinks on Windows, applies `patches\rexglue-sdk\*.patch`, builds and installs the SDK, and
builds `tools\analysis\lzxdec.exe`. Pass `-Sdk <path>` to put the SDK somewhere else.

| Patch | What it does |
|---|---|
| 0001 | Builds the USB camera kernel exports the game imports |
| 0002 | Finds a module loaded by bare file name (`fifadllzf.xex`) |
| 0003 | `function_table_base`: moves a module's dispatch table so `default.xex` and `fifadllzf.xex` don't collide |
| 0004 | Precise guest vblank timing (high-resolution timer instead of `Sleep(1)`) |
| 0005 | Lets `game_data_root` be an Xbox 360 disc image (`.iso`) |

## 2. Generate and build the game

```powershell
tools\build\build.ps1 -Game "<path to your .iso or game folder>"
```

Extracts `default.xex` and `fifadllzf.xex` to `..\game-x360`, runs the ReXGlue code generator
(`x360\fifawc10_manifest.toml` plus `x360\config\*.toml`) and builds
`x360\out\build\win-amd64-release\fifawc10.exe`. `-Config relwithdebinfo` gives a build with debug info.
Later runs only regenerate what changed.

`x360\generated\` is created from the game binary. It stays on your PC and is git-ignored.

## 3. Package

```powershell
tools\build\package.ps1
```

Publishes the launcher and puts the game, the launcher, the Visual C++ runtime and the licenses in
`out\package\FIFA World Cup 2010 Recompiled\`, plus `out\FIFA-World-Cup-2010-Recompiled-<version>-win-x64.zip`.
The version comes from `VERSION` (also set in `launcher\Launcher.csproj`).

## Project layout

| Path | What |
|---|---|
| `x360/fifawc10_manifest.toml` | Codegen manifest: `default.xex` and the `fifadllzf.xex` game module |
| `x360/config/` | Analysis overrides: missed function starts, code pointers, setjmp/longjmp |
| `x360/src/` | The Windows app: paths, crash handler, launcher hand-off, developer switches |
| `x360/src/module/` | Hooks compiled into the game module's DLL (boot fix, Lua error log) |
| `x360/res/` | Icon and version resource of `fifawc10.exe` |
| `launcher/` | WPF launcher: game check, settings, start (`Theme.xaml` holds the look) |
| `patches/rexglue-sdk/` | SDK patches |
| `tools/build/` | `setup_sdk.ps1`, `build.ps1`, `package.ps1`, `fix_symlinks.py`, `make_icon.ps1` |
| `tools/analysis/` | Reverse-engineering helpers (below) |
| `tools/dev/` | Automated test runs (below) |
| `tools/disc.py` | Detect, list and extract Xbox 360 and PS3 disc images |

## Analysis tools (`tools/analysis/`)

| Tool | Use |
|---|---|
| `xexinfo.py`, `xeximage.py` | XEX headers; decrypted, decompressed memory image (LZX through `lzxdec.exe`) |
| `ppcdis.py` | PowerPC disassembly of an image, PE sections, `.pdata` lookup |
| `autofix_codegen.py` | Re-runs codegen and adds missed function starts until analysis passes |
| `find_code_pointers.py` | Finds functions only reached through pointers (vtables, callbacks) |
| `find_setjmp.py`, `xref.py`, `callers.py` | Locate setjmp/longjmp, cross-references, callers |

## Test scripts (`tools/dev/`)

They run the game, drive it with the keyboard and save screenshots to `out\dev\`.
The game copy is `$env:FIFAWC10_GAME`, or the first `.iso` next to the repository.

| Script | Use |
|---|---|
| `run_game.ps1` | Start `fifawc10.exe` (`-Config release`, `relwithdebinfo` or `package`), logging to `out\dev\run.log` |
| `to_match.ps1` | Fresh profile, menus, into a match; screenshots on the way |
| `nav.ps1` | Key steps with a screenshot after each (`Return:8`, `Space:4`, `wait:10`, `shot`) |
| `sheet.ps1` | Contact sheet of a run's screenshots |
| `launcher_shots.ps1` | Screenshot every launcher tab |
| `press.ps1`, `focus.ps1`, `screenshot.ps1`, `click_play.ps1` | Building blocks |

Keyboard input only reaches the game while its window has focus, and with a gamepad connected the pad wins.

## Developer switches

Environment variables read by `fifawc10.exe`:

- `FIFAWC10_PERF_LOG=1` logs the frame rate once a second
- `FIFAWC10_WATCH=<address>` logs every guest write to a word, with the host stack
