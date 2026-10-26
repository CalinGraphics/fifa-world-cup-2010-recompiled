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
   applies the [SDK patches](patches/rexglue-sdk/README.md), then builds and installs it.
2. Takes `default.xex` and `fifadllzf.xex` from your disc image into `out\game-files\`.
3. Runs the ReXGlue code generator (`pc\fifawc10_manifest.toml` and `pc\config\*.toml`) and builds
   `out\build\win-amd64-release\fifawc10.exe`. Later runs only regenerate and rebuild what changed.
4. With `-Package` (`installer\package.ps1`): publishes the launcher and puts the game, the launcher, the
   Visual C++ runtime and the licenses in `out\package\FIFA World Cup 2010 Recompiled\`, makes
   `out\FIFA-World-Cup-2010-Recompiled-<version>-win-x64.zip`, and copies the game and launcher into the
   repository folder, so you can play from there.

Options: `-Game <path>` for a disc image or game folder somewhere else, `-Config relwithdebinfo` for a build
with debug info.

Everything the build makes (`out\`, `pc\generated\`, the `.exe` and `.dll` files) stays on your PC and is
git-ignored, like the SDK checkout. The version comes from `VERSION` (also set in `launcher\Launcher.csproj`).

## Settings the game needs

Set by the game itself (`pc\src\fifawc10_app.h`) unless the config file says otherwise:

| Setting | Why |
|---|---|
| `execute_unclipped_draw_vs_on_cpu = true` | The game draws full-screen passes with clipping off. Estimated from the scissor, they claimed the whole render target memory and the screen went black for seconds at a time (Training Ground). |
| `gpu_allow_invalid_fetch_constants = true` | Many textures are bound with fetch constants typed "invalid", which the console samples normally. Without it they render black. |

## Repository layout

| Path | What |
|---|---|
| `build.ps1` | The build, start to finish |
| `pc/fifawc10_manifest.toml` | Codegen manifest: `default.xex` and the `fifadllzf.xex` game module |
| `pc/config/` | Analysis overrides: missed function starts, code pointers, setjmp/longjmp |
| `pc/src/` | The Windows app: paths, game settings, FPS counter, crash handler, launcher hand-off |
| `pc/fixes/` | Hooks compiled into the game module's DLL (boot fix, Lua error log) |
| `pc/res/` | Icon and version resource of `fifawc10.exe` |
| `launcher/` | WPF launcher: game check, settings, start (`Theme.xaml` holds the look) |
| `installer/` | Release packaging (`package.ps1`) and the player readme shipped in the zip |
| `patches/rexglue-sdk/` | SDK patches, with their own [README](patches/rexglue-sdk/README.md) |
| `tools/` | SDK setup, disc images (`disc.py`), and the reverse-engineering helpers below |
| `.github/` | Issue form |

## Tools (`tools/`)

| Tool | Use |
|---|---|
| `setup_sdk.ps1`, `fix_symlinks.py` | Get the SDK ready (run by `build.ps1`) |
| `disc.py` | Detect, list and extract Xbox 360 and PS3 disc images |
| `make_icon.ps1` | Draws `launcher\icon.ico` |
| `xexinfo.py`, `xeximage.py` | XEX headers; decrypted, decompressed memory image (LZX through `lzxdec.exe`, from `lzxdec\`) |
| `ppcdis.py` | PowerPC disassembly of an image, PE sections, `.pdata` lookup |
| `autofix_codegen.py` | Re-runs codegen and adds missed function starts until analysis passes |
| `find_code_pointers.py` | Finds functions only reached through pointers (vtables, callbacks) |
| `find_setjmp.py`, `xref.py`, `callers.py` | Locate setjmp/longjmp, cross-references, callers |

## Releasing an update

The launcher checks the latest GitHub release when it opens. For it to offer an update:

1. Raise `VERSION` and `<Version>` in `launcher\Launcher.csproj`, then `build.ps1 -Package`.
2. Create a release tagged `v<version>` (e.g. `v0.3.2`) and attach `FIFA-World-Cup-2010-Recompiled-<version>-win-x64.zip`.
3. Put the zip's SHA-256 in the release notes: the launcher checks the download against it.

## Developer switches

Environment variables read by `fifawc10.exe`:

- `FIFAWC10_PERF_LOG=1` logs the frame rate once a second
- `FIFAWC10_WATCH=<address>` logs every guest write to a word, with the host stack
- `FIFAWC10_UPDATE_FEED=<url>` (launcher) reads the release from that URL instead of GitHub, for testing an update
