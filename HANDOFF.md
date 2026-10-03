# Drone Launcher technical handoff

Scope: close out the current native implementation and continue future work from `origin/main` at https://github.com/StillJC/DroneLauncher. No original game payload is in Git. Source locations below are repository-relative. Detailed verification: [feature status](docs/FEATURE_STATUS.md) and [acceptance](docs/ACCEPTANCE.md).

## A. Current status

| Classification | Subsystems |
|---|---|
| WORKING / VERIFIED | Launcher/root discovery; direct Shell startup and checksum handoff; original 94-file CRC; x86 IO; Unity startup and native path/input compatibility; INI/GameData redirects; keyboard; Steering, Vertical Dive/Climb, Trigger/Confirm/Boost, Start, Coin, Service, Test; normal shutdown; operator/relaunch; original audio settings; standalone race; tested display modes/quality/monitor placement; tested spaces/secondary-drive path |
| IMPLEMENTED / PARTIALLY VERIFIED | XInput/generic joystick (physical endpoints passed, latest capture/tuning/reconnect coverage incomplete); single-page control editor; live test; Exit binding (logical controller state and UI persistence passed, physical prompt and race-key coverage incomplete); native outputs/rumble/TCP (actual events tested, fatal-exit/reconnect coverage incomplete); LAN settings (no two-peer gameplay); OS DPI scaling (layout simulations only) |
| INCOMPLETE | Fatal Unity/Shell termination acceptance, LAN gameplay acceptance, final physical review of revised controls UI, longer soak/OS DPI testing |
| NOT IMPLEMENTED | Generic joystick force feedback; universal local cabinet-output driver; proven camera switch or Secondary-as-Back; reliable exclusive fullscreen |

Secondary is verified as the original raw button, not a proven selection-menu Back action. See the per-feature table for exact scope; implementation does not imply hardware acceptance.

## B. Repository structure

All production source stays under `Development/loader-src/` to preserve project references:

| Exact path | Purpose |
|---|---|
| `Development/loader-src/DroneRacingGenesisLoader/DroneRacingGenesisLoader.csproj` | Main C# WinForms project; `Program.cs` -> `Application.Run(new LoaderForm(...))` |
| `Development/loader-src/DroneRacingGenesisLoader/Installation.cs` | EXE-derived roots, direct/nested layout, preflight |
| `Development/loader-src/DroneRacingGenesisLoader/RuntimeConfiguration.cs` | Path generation, control/security models, network field updates |
| `Development/loader-src/DroneRacingGenesisLoader/ProcessMonitor.cs` | Ownership, child discovery, lifecycle, shutdown |
| `Development/loader-src/DroneRacingGenesisLoader/PhysicalInputManager.cs` | Shared keyboard/XInput/WinMM sampler |
| `Development/loader-src/DroneRacingGenesisLoader/OutputManager.cs` | Output model, native event consumer, rumble, TCP |
| `Development/loader-src/DroneRacingGenesisLoader/DisplayManager.cs` | Graphics model, enumeration, arguments, placement |
| `Development/loader-src/DroneRacingGenesisLoader/FeatureSettings.cs` | Display/network/output forms and NetworkConfiguration |
| `Development/loader-src/DroneRacingGenesis.Plugin/` | Own managed `PortablePathMap.cs`, `ShellSecurityWorkaround.cs`, netstandard2.1 project |
| `Development/loader-src/IOCompatibility/io_module.cpp` | Native x86 IO, outputs, exact-game CreateProcess adaptation |
| `Development/loader-src/IOCompatibility/bootstrap.cpp` | x86 suspended Shell bootstrap |
| `Development/loader-src/NativeUnity/unity.cpp` | x64 guarded path/input/optional graphics compatibility |
| `Development/loader-src/NativeUnity/injector.cpp` | x64 Unity bootstrap |
| `Development/loader-src/NativeUnity/common.h` | Native utilities/hash helper shared by both native components |
| `Development/loader-src/NativeUnity/guard_test.cpp` | Optional development rejection-test host; requires own runtime |
| `Development/loader-src/DroneRacingGenesisLoader.Tests/` | Source regression executable with generated temporary fixtures |
| `Development/loader-src/DroneRacingGenesisLoader/Assets/` | Supplied launcher logo/icon only |
| `config/` | Sanitized release defaults, not mutable cabinet state |
| `docs/` | Build/release/configuration/test/audit documentation |
| `build.ps1`, `global.json`, both native `build.cmd` | Complete build entrypoint, SDK selection, native compilation; no solution/CMake/Ninja required |

Runtime, research captures, frozen rollback copies, obsolete Frida scripts and build outputs remain ignored locally. They are not fresh-clone dependencies.

## C. Fresh-clone build

```powershell
git clone https://github.com/StillJC/DroneLauncher.git
cd DroneLauncher
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Windows x64; .NET SDK 8.0.420 (global.json permits later patches in its feature band); Visual Studio C++ x86/x64 desktop tools and Windows SDK. Tested: VS Community 2026 18.10.2, MSVC 14.51.36231, SDK 10.0.26100.0. See [BUILD.md](docs/BUILD.md) for exact commands and verification. No game files are needed to build/test the source fixtures. A normal network-connected NuGet restore is required on first build.

Output: `artifacts/Runtime/DroneLauncher.exe` (self-contained .NET 8, x64); IO DLL/bootstrap x86; Unity DLL/bootstrap x64. Native `/MT`, `/O2`, C++17; no independent VC runtime installation for these helpers. The original game's own prerequisites are separate.

## D. Runtime layout and ownership

```text
Runtime/
  DroneLauncher.exe                    [built here]
  Launcher/
    Config/*.json                      [shipped defaults; user-edited afterward]
    Config/Original/                   [runtime-created backups; never publish]
    Logs/Loader/                       [runtime-created; never publish]
    Plugins/IO/                        [built x86 DLL/bootstrap]
    Plugins/Unity/                     [built x64 DLL/bootstrap]
    Licenses/                          [resolved Microsoft runtime-pack notices]
    README.md, THIRD_PARTY_NOTICES.md   [launcher documents]
  Shell/                               [user-supplied original installation]
  ShellData/                           [user-supplied; mutable operator settings]
  GameData/                            [user-supplied; mutable saves/scores]
  DroneRacing/                         [user-supplied original Unity game]
  backup/                              [user-supplied original backup metadata]
```

Copy/merge only the launcher payload into a copy of your own matching runtime; preserve existing Config during upgrades. Original Shell scans Runtime root for eligible game directories. Never add arbitrary build/docs/test directories there. Parent paths containing `shell`, arbitrary Unicode and extended-length paths are not supported claims because original ANSI/scanner code remains.

## E. Startup chain

```text
DroneLauncher.exe
  -> DroneRacingGenesis.IOBootstrap.exe (x86)
     -> Shell.exe /k=186CD76A (initially suspended)
        + DroneRacingGenesis.IO.dll initialized, then Shell resumes
        -> DroneRacing.exe (original child creation, suspended)
           -> DroneRacingGenesis.UnityBootstrap.exe (x64)
              + DroneRacingGenesis.Unity.dll initialized in game
           -> original game main thread resumes after successful initialization
```

Architecture-specific bootstraps initialize compatibility inside matching processes. Shell remains the original lifecycle/CRC/operator owner. Suspending Unity prevents early absolute-path consumers from running before guarded hooks initialize. Initial loader-lock/remote module setup is in `NativeUnity/injector.cpp`. Bootstrap failure prevents the new child from resuming and reports a PID-scoped error. Ordinary startup bypasses SystemLauncher, whose installer/update responsibilities remain outside this launcher.

## F. Shell compatibility

Supported Shell SHA256: `59BF7C7676A4AAEC584B4FAF81CDBAF36A6F9B267E69BC0D01250989053AF82B`.

`ShellStartupArguments.cs` validates the build and computes `/k=186CD76A`; `ProcessMonitor.cs` sets Shell's working directory explicitly. `RuntimeConfiguration.Generate` backs up Game.ini, rewrites only understood paths, preserves config.ini `Directory=.\variables`, and uses `ShellSecurityWorkaround.Configure` to request original `[Debug] SecurityDisabled=1`. The supplied DK2 DLL is not replaced or disk-patched. This built-in setting does not remove the independent IO startup gate.

`IOCompatibility/bootstrap.cpp` and `io_module.cpp` provide the known MkII cabinet HID/setup/ReadFile/WriteFile interface and original E0/E1/F0/F1 handshake. They do not force IOReady, state 32, CRC success or operator state. Build-specific IAT addresses and byte checks must be retained. Initialization failure is handled before resuming Shell, rather than letting partially initialized Shell run.

The output observer sits at the existing original write dispatch. Only the exact owned game's CreateProcess is adapted for native initialization and managed graphics arguments. Other process creation remains original. TEST enters original state 34; operator EXIT starts a fresh game under the same Shell.

## G. Unity compatibility

Supported GameAssembly SHA256: `491F7C3E3AB392F78AEA8B3B9775B65AAD63ACDE2AE4E2FC4D04FDFE716FB652`.

`UnityPortability.cs` checks the whole file before startup; `NativeUnity/unity.cpp` checks it again plus every path-call signature, IOManager prologue, mapping version, and optional graphics call signatures **before installation**. Eight narrow sites redirect ShellData.ini, GameSettings.ini and GameData Exists/Load/Save/Delete directory arguments. Only matching original path strings at known call sites are replaced; retained IL2CPP GC handles protect replacement strings. There is no global filesystem hook.

IOManager.update restores the previous original sample before the original method, then applies the shared local sample afterward. No network input-array writes or second physical polling backend in Unity. A stale (>2 seconds), incoherent, missing or wrong-version sample falls back to original input. Guard failure rejects initialization; install-write failures attempt restoration of changed call sites and the bootstrap prevents a failed new child from running. No disk GameAssembly patch.

Optional RootScene resolution/quality wrappers are separately signature-guarded. Configure with `Launcher/Config/unity-portability.json` and optional `graphics.json`.

## H. Input architecture

`PhysicalInputManager` is the one physical-input implementation. During a game session one broker samples every ~8 ms independently of the thread pool. The 64-byte mapping uses sequence coherence, millisecond heartbeat, layout version **1** at offset 30, original cabinet report and eight float values. JSON control schema is version **2**; do not confuse them.

Keyboard uses Windows key states scoped to loader/Shell/game foreground. XInput polls four slots. WinMM polls up to sixteen joystick IDs, six axes, buttons and POV. Capture prefers XInput when the same pad has duplicate interfaces. Generic WinMM IDs are enumeration indices, not stable hardware identities.

Shell consumes original cabinet bytes; Unity consumes the same broker's normalized values. Live Test Controls uses that implementation; offline preview constructs a broker from the edited configuration. Exit is a rising edge in the launcher, suppressed in test/capture. Capture ignores initially held keys, permits re-press after release and stops its timer before proceeding to the next direction.

Public functions: Steering Left/Right; Vertical Up=Dive/Down=Climb; Trigger/Confirm/Boost; Start; Secondary; Coin; Service; Test; Exit. A flight axis can instead use two keys/buttons; opposing inputs cancel. Editor double-click captures immediately, replaces only that function and preserves other/hidden/custom bindings. Save is atomic with `.previous` backup. Changes apply next session. Test while running shows active bindings.

Secondary is not original SelectFlow Back. View is not proven to switch cameras; old View/Throttle bindings remain preserved but hidden. Original local race forces acceleration=1 and brake=0. Do not infer new gameplay semantics from those names.

## I. Outputs

Original Shell write dispatch -> optional 256-event version-1 shared ring -> `OutputManager` -> optional XInput and TCP. Producer never waits for an external receiver. Handshake commands are excluded. Verified binary names: `vibration`, `billboard`, `controller_red/green/blue`, `footwell_red/green/blue`, `monitor_lower_red/green/blue`. Unknown Edge/Back/Base pattern selections remain diagnostics, not public output names.

Rumble uses selected XInput slot and strength; both motors receive the same original vibration level. No input-synthesized effects. Session close, game end, lost output heartbeat and disposal request zero. Physical ON/OFF passed; fatal cleanup/reconnect validation remains incomplete. Generic force feedback and universal local HID outputs are not implemented.

TCP is outbound UTF-8 NDJSON: `{"version":1,"sequence":12,"time":"...UTC...","output":"vibration","value":1}`. Changed states and one-second refresh; current state on reconnect; bounded 64-line drop-oldest queue; one-second deadlines/two-second reconnect. Incoming bytes are ignored. This is a state stream, not guaranteed pulse delivery. Both backends default disabled in `outputs.json`.

## J. Networking

`RuntimeConfiguration.GenerateNetwork` and `FeatureSettings.Network` coordinate `network.json` with original operator LinkPlay, CabinetID, NumCabinets. Standalone writes 0/1/1; LAN applies selected original fields once (`ApplyOnNextLaunch`) and preserves later operator changes. Legacy Original restores backed-up network fields only. No firewall rules are added.

Observed original services: Shell local game TCP **18888**, Shell cabinet TCP **27105**; Unity cabinet host/discovery UDP **8213** in prior linked-mode observation. INI CabinetPort 1234 had no established socket consumer. Original local TCP remains active in standalone. LAN configuration preservation passed source tests; there has been **no real second peer**, so discovery, synchronization, race starts and link reconnect are unverified. Do not treat an open socket as proof of LAN gameplay.

## K. Graphics / monitor

`DisplayManager` uses Screen.AllScreens and EnumDisplaySettings, stores Windows display DeviceName and falls back to Primary with a warning if absent. Child CLI merges width/height/fullscreen/window-mode/monitor/screen-quality without duplicate managed options. Exact-owned window placement supplies a bounded Windows fallback for Unity and Shell; persistent settings do not store desktop coordinates.

Original RootScene overwrites early resolution/quality; optional guarded wrappers apply final settings. Quality presets: Very Low, Low, Medium, High, Very High, Ultra; actual original default Very High (serialized default was Very Low). Original VSync=1 and targetFrameRate=60. Each quality index was observed after application. Windowed/Borderless on primary/non-primary and repeated operator relaunch were tested. Exclusive requests resolved to Borderless on both displays and are omitted from UI. Missing-monitor fallback passed at a secondary-drive long path. See [ACCEPTANCE.md](docs/ACCEPTANCE.md) for the exact matrix.

## L. Configuration files

All paths below are relative to ContentRoot. Build payload seeds `Launcher/Config` from committed `config/` **only when absent**. These files are launcher-owned; original ShellData is never a source default.

| Path | Purpose / shipped default | Rewrite / activation |
|---|---|---|
| `Launcher/Config/controls.json` | schema 2; enabled native input; arrows/XInput, shared trigger; Escape Exit with confirmation; no specific generic device ID | Editor atomic Save + previous backup; next session. If omitted, EnsureControls creates inactive empty bindings, not this release profile |
| `Launcher/Config/shell-compatibility.json` | EnableSecurityWorkaround=true; EnableIOCompatibility=true | Created if absent with IO **false**; use supplied release default; next launch |
| `Launcher/Config/unity-portability.json` | Enabled=true; Engine=Native informational legacy field | Required native launch config; manually edited; next launch |
| `Launcher/Config/network.json` | Mode=Standalone; CabinetID=1; NumCabinets=2 (LAN selection); ApplyOnNextLaunch=false | UI Save; LAN clears apply flag after updating original keys; next launch; no file means original settings retained |
| `Launcher/Config/graphics.json` | Enabled=false; Monitor empty/Primary fallback; 1920x1080 Borderless; Quality=-1 original | UI Save; next session; absent/invalid falls back safely |
| `Launcher/Config/outputs.json` | RumbleEnabled=false; slot0, 40%; TcpEnabled=false; loopback:8000 inert placeholder | UI Save; next session; missing/invalid disables optional output use |
| `Launcher/Config/Original/*` | Initial original Game.ini, network master and migrated controls backup | Generated from user's runtime as needed; not source/release files |

Historical local `network-observation.json` is unused research state; it is neither a production requirement nor distributed. `.previous`, `.tmp` and original backup files are not templates. Existing cabinet audio/calibration/credits are preserved when generating understood paths/network fields.

## M. UI source map

These forms are **entirely constructed in C#**; there are no `.Designer.cs` or `.resx` companions to synchronize.

| UI area | Exact file / class or method |
|---|---|
| Startup/main/status | `Development/loader-src/DroneRacingGenesisLoader/Program.cs`; `LoaderForm.cs` / LoaderForm, statusLabel, FriendlyStatus |
| Configure Controls | `Development/loader-src/DroneRacingGenesisLoader/ControlsForm.cs` / ControlsForm |
| Test Controls, capture | `Development/loader-src/DroneRacingGenesisLoader/ControlTestForm.cs` / ControlTestForm, BindingCaptureForm |
| Display / Graphics | `Development/loader-src/DroneRacingGenesisLoader/FeatureSettings.cs` / Display |
| Monitor enumeration/placement | `Development/loader-src/DroneRacingGenesisLoader/DisplayManager.cs` |
| Network / LAN | `Development/loader-src/DroneRacingGenesisLoader/FeatureSettings.cs` / Network; `RuntimeConfiguration.cs` / GenerateNetwork |
| Outputs | `Development/loader-src/DroneRacingGenesisLoader/FeatureSettings.cs` / Outputs; `OutputManager.cs` |
| Audio Settings / Operator button | `Development/loader-src/DroneRacingGenesisLoader/LoaderForm.cs` / constructor callbacks, RequestOperator; original Shell owns pages |
| Exit binding/confirmation | `PhysicalInputManager.cs` / ExitPresses; `LoaderForm.cs` / CheckExit, RequestExit; `ControlsForm.cs` / Exit row and checkbox, all in main project directory |
| Settings helpers | `Development/loader-src/DroneRacingGenesisLoader/FeatureSettings.cs` / Dialog, Row, Note, Choice, Load, Save |
| Shared styling/resources | `Development/loader-src/DroneRacingGenesisLoader/ControlTestForm.cs` / LauncherStyle; main project `Assets/DroneLauncher.png` and `.ico` |

## N. Branding

Product/title **Drone Launcher**, EXE/assembly **DroneLauncher**, namespace remains **DroneRacingGenesisLoader**. Supplied original Development logo/icon were copied byte-for-byte into the main project's Assets. The csproj embeds `DroneLauncher.Logo` and `DroneLauncher.Icon`; ApplicationIcon supplies the EXE icon; LauncherStyle.Apply sets form/taskbar icon and Logo loads the embedded bitmap. Runtime never reads Development. Owner-created artwork and source are MIT-licensed; third-party notices are documented in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## O. Do not break

- Keep Runtime root scanner restrictions; additions belong in Launcher.
- No original game/Shell binary or manifest disk patch; preserve original CRC and supported hashes/signatures.
- Shell owns normal child and TEST/operator lifecycle; initialize compatibility before original child resume.
- No broad/global filesystem redirects or network input-array writes.
- One physical-input implementation and one session broker; no second Unity polling stack.
- Self-contained .NET deployment and architecture-matched static-CRT helpers.
- Graceful TEST/window close before bounded failure termination; reject unsupported builds safely.
- Preserve original automatic acceleration/no braking unless a future feature explicitly changes that behavior.
- Keep optional outputs nonblocking and disableable, with shutdown zero; preserve user configs on upgrade.

## P. Prioritized remaining work

**HIGH**

- Deliberate bounded Unity/Shell failure tests (owned-process cleanup, rumble zero, usable launcher).
- Complete real two-machine LAN testing before advertising linked gameplay.

**MEDIUM**

- Latest double-click UI physical capture review; keyboard/controller Exit in all gameplay contexts.
- Hands-on tuning and rumble unplug/reconnect. Physical axis endpoints already passed for Xbox and generic clone; do not repeat enumeration as supposed physical proof.
- Investigate three transient input fallbacks seen in prior D: run without widening guards.
- Generic device identity/reconnection coverage and actual Windows DPI transitions; currently indices and simulated scaling.

**LOW / POLISH**

- UI wording/spacing and independent visual review; longer soak testing.
- Formal binary release packaging with MIT/runtime notices; source handoff is not a binary release.

Final component hashes and clean-build/smoke results: [BUILD.md](docs/BUILD.md), [component hashes](docs/COMPONENT_HASHES.json), [ACCEPTANCE.md](docs/ACCEPTANCE.md). Local historical research remains ignored; this handoff contains the information needed for fresh-clone development.
