# ReXGlue SDK patches

Base: [rexglue-sdk](https://github.com/rexglue/rexglue-sdk) at commit `bd833a2`.

`tools\setup_sdk.ps1` (run by `build.ps1` the first time) clones the SDK without CRLF conversion,
checks out that commit, applies these patches in order and builds it. By hand:

```
git clone -c core.autocrlf=false https://github.com/rexglue/rexglue-sdk
cd rexglue-sdk
git checkout bd833a2
git submodule update --init --recursive
git apply <this repo>/patches/rexglue-sdk/*.patch
cmake --preset win-amd64
cmake --build out/build/win-amd64 --config Release --target install
```

| Patch | What it does |
|---|---|
| 0001 build-usbcam-exports | Builds the USB camera kernel exports the game imports |
| 0002 find-module-by-file-name | Finds a module loaded by bare file name (`fifadllzf.xex`) |
| 0003 function-table-base | `function_table_base`: moves a module's dispatch table so `default.xex` and `fifadllzf.xex` don't collide |
| 0004 precise-vblank | Precise guest vblank timing (high-resolution timer instead of `Sleep(1)`) |
| 0005 disc-image-game-root | Lets `game_data_root` be an Xbox 360 disc image (`.iso`) |
| 0006 frame-rate-limit-and-swap-count | `frame_rate_limit` (the launcher's 30 fps option) and a count of presented frames for the FPS counter |
| 0007 dialogs-without-continuous-repaint | Overlays that only change with the game's frames don't keep the window repainting at the display's refresh rate |
| 0008 aggregate-enumerator-user-saves | `XamContentAggregateCreateEnumerator` also lists the signed-in user's saves when the game asks for any user, any content type, or the xuid cut to its low 32 bits (this game does that). The game found none, asked for the profile setup on every start and wrote a new save file each time |
