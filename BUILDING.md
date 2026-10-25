# Building

## Requirements

- Windows 10 or 11 (x64)
- Visual Studio 2022 or newer with the C++ workload (Windows SDK, CMake, Ninja)
- LLVM/clang 18 or newer: `winget install LLVM.LLVM`
- Python 3.9+ with `cryptography` (and `capstone` for the analysis tools): `pip install cryptography capstone`
- Git and the .NET 10 SDK (for the launcher)
- Your own **Xbox 360** copy of the game (Europe disc, En/Es/Nl) as an `.iso`

## Build

Put your `.iso` in the repository folder (it's git-ignored), then:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -Package
```

That's the whole build. `build.ps1`:

1. The first time, gets the ReXGlue SDK ready in `rexglue-sdk\` (`tools\setup_sdk.ps1`): clones
   [ReXGlue](https://github.com/rexglue/rexglue-sdk), pins commit `bd833a2`, fixes git symlinks on Windows,
   applies `patches\rexglue-sdk\*.patch`, then builds and installs it.
2. Takes `default.xex` and `fifadllzf.xex` from your disc image into `out\game-files\`.
3. Runs the ReXGlue code generator (`recomp\fifawc10_manifest.toml` and `recomp\config\*.toml`) and builds
   `out\build\win-amd64-release\fifawc10.exe`. Later runs only regenerate and rebuild what changed.
4. With `-Package`: publishes the launcher and puts the game, the launcher, the Visual C++ runtime and the
   licenses in `out\package\FIFA World Cup 2010 Recompiled\`, plus
   `out\FIFA-World-Cup-2010-Recompiled-<version>-win-x64.zip`.

Options: `-Game <path>` for a disc image or game folder somewhere else, `-Config relwithdebinfo` for a build
with debug info.

Everything the build makes from your game (`out\`, `recomp\generated\`) stays on your PC and is git-ignored,
like the SDK checkout. The version comes from `VERSION` (also set in `launcher\Launcher.csproj`).

## SDK patches

| Patch | What it does |
|---|---|
| 0001 | Builds the USB camera kernel exports the game imports |
| 0002 | Finds a module loaded by bare file name (`fifadllzf.xex`) |
| 0003 | `function_table_base`: moves a module's dispatch table so `default.xex` and `fifadllzf.xex` don't collide |
| 0004 | Precise guest vblank timing (high-resolution timer instead of `Sleep(1)`) |
| 0005 | Lets `game_data_root` be an Xbox 360 disc image (`.iso`) |
| 0006 | `frame_rate_limit` (the launcher's 30 fps option) and a count of presented frames for the FPS counter |
| 0007 | Overlays that only change with the game's frames don't keep the window repainting at the display's refresh rate |

## Settings the game needs

Set by the game itself (`recomp\src\fifawc10_app.h`) unless the config file says otherwise:

| Setting | Why |
|---|---|
| `execute_unclipped_draw_vs_on_cpu = true` | The game draws full-screen passes with clipping off. Estimated from the scissor, they claimed the whole render target memory and the screen went black for seconds at a time (Training Ground). |
| `gpu_allow_invalid_fetch_constants = true` | Many textures are bound with fetch constants typed "invalid", which the console samples normally. Without it they render black. |

## Repository layout

| Path | What |
|---|---|
| `build.ps1` | The build, start to finish |
| `recomp/fifawc10_manifest.toml` | Codegen manifest: `default.xex` and the `fifadllzf.xex` game module |
| `recomp/config/` | Analysis overrides: missed function starts, code pointers, setjmp/longjmp |
| `recomp/src/` | The Windows app: paths, game settings, FPS counter, crash handler, launcher hand-off |
| `recomp/src/module/` | Hooks compiled into the game module's DLL (boot fix, Lua error log) |
| `recomp/res/` | Icon and version resource of `fifawc10.exe` |
| `launcher/` | WPF launcher: game check, settings, start (`Theme.xaml` holds the look) |
| `patches/rexglue-sdk/` | SDK patches |
| `tools/` | `setup_sdk.ps1`, `package.ps1`, `disc.py` (Xbox 360 / PS3 disc images), `fix_symlinks.py`, `make_icon.ps1` |
| `tools/analysis/` | Reverse-engineering helpers (below) |
| `tools/dev/` | Automated test runs (below) |

Local only (git-ignored): `rexglue-sdk/`, `out/`, `recomp/generated/`, your `.iso`.

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
The game copy is `$env:FIFAWC10_GAME`, or the first `.iso` in the repository folder.

| Script | Use |
|---|---|
| `run_game.ps1` | Start `fifawc10.exe` (`-Config release`, `relwithdebinfo` or `package`), logging to `out\dev\run.log` |
| `to_match.ps1` | Fresh profile, menus, into a match; screenshots on the way |
| `to_training.ps1` | Fresh profile, menus, into Training Ground |
| `black_test.ps1` | Training Ground with given switches, sampled once a second for black frames |
| `burst.ps1` | Screenshots in quick succession, counting black ones |
| `nav.ps1` | Key steps with a screenshot after each (`Return:8`, `Space:4`, `wait:10`, `shot`) |
| `sheet.ps1` | Contact sheet of a run's screenshots |
| `launcher_shots.ps1` | Screenshot every launcher tab |
| `press.ps1`, `focus.ps1`, `screenshot.ps1`, `click_play.ps1` | Building blocks |

Keyboard input only reaches the game while its window has focus, and with a gamepad connected the pad wins.

## Developer switches

Environment variables read by `fifawc10.exe`:

- `FIFAWC10_PERF_LOG=1` logs the frame rate once a second
- `FIFAWC10_WATCH=<address>` logs every guest write to a word, with the host stack
