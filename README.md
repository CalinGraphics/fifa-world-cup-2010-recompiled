# 2010 FIFA World Cup South Africa: Recompiled

An unofficial native PC port of the Xbox 360 game *2010 FIFA World Cup South Africa*.
The game's PowerPC code is translated to C++ with [ReXGlue](https://github.com/rexglue/rexglue-sdk)
and compiled into a Windows program with a Direct3D 12 renderer. No emulator.

![Launcher](docs/images/launcher.png)

## Status

Playable: menus, profile setup, team and kit selection, match intros and full matches.

- 60 fps in matches (menus and cutscenes run at the game's own 30 fps)
- Render resolution up to 4K (3x the original 720p)
- Anisotropic filtering up to 16x
- Runs straight from your disc image (`.iso`): nothing to extract
- Controller (XInput) or keyboard

Not tested yet: online modes, every game mode end to end, other regions.
Report problems in [Issues](../../issues) with the log from the launcher's "Saves and logs folder".

## You need your own copy of the game

**No game files are included in this repository or its builds, and none ever will be.**
You need the **Xbox 360** version, dumped from your own disc: Europe, languages En/Es/Nl.
The launcher checks the copy and tells you if it's a different version.

The PS3 version is detected but not supported yet.

## Playing

1. Build the project (see [BUILDING.md](BUILDING.md)) or use a build you made with `tools\package.ps1`.
2. Run `Launcher.exe`, click **Disc image (.iso)** and pick your copy.
3. Choose your settings and press **PLAY**.

Keyboard controls and other details are in [docs/PLAYING.txt](docs/PLAYING.txt).

Requirements: Windows 10 or 11 (64-bit), a Direct3D 12 graphics card.

## Building

See [BUILDING.md](BUILDING.md). In short:

```
tools\setup_sdk.ps1
tools\build.ps1 -Game "<your .iso>"
tools\package.ps1
```

## How it works

- `default.xex` is a small loader; the game itself is the `fifadllzf.xex` module. Both are
  recompiled; the module becomes `fifawc10_fifadllzf.dll`.
- `x360/config/` holds what the analyser can't work out alone: functions only reached by
  tail jumps or through pointers, and the CRT's setjmp/longjmp used by Lua error handling.
- `x360/src/module/boot_fixes.cpp` works around a read of a not-yet-created object on the
  first front-end frame, which the console tolerates and a PC does not.
- Five small ReXGlue patches (`patches/rexglue-sdk/`): module loading by bare name,
  dispatch table placement for two modules, precise vblank timing, and disc image support.

## Credits

- The [ReXGlue](https://github.com/rexglue/rexglue-sdk) team for the recompiler and runtime.
- The [Xenia](https://xenia.jp) project, whose Xbox 360 research ReXGlue builds on.

## License

This project's own code is under the BSD 3-Clause license (see [LICENSE](LICENSE)).
It does not cover the game, its content, or code recompiled from it.
Third-party components keep their own licenses.

This is an unofficial fan project, not affiliated with or endorsed by EA, FIFA, Microsoft or Sony.
