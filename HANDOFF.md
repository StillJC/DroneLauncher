# Drone Launcher technical handoff

Drone Launcher is a Windows launcher and native compatibility layer for the supported **Drone Racing Genesis** installation.

Repository:

```text
https://github.com/StillJC/DroneLauncher
```

Production source is under:

```text
Development/loader-src/
```

No original game payload is stored in Git.

This document describes the current 1.0 implementation and the design constraints that should be preserved during future development.

Detailed public status is also recorded in:

```text
docs/FEATURE_STATUS.md
docs/ACCEPTANCE.md
docs/RUNTIME.md
docs/BUILD.md
```

## A. Current status

The primary 1.0 feature set is working on real hardware.

Verified functionality includes:

- direct launcher startup
- original Shell startup
- original Shell/game lifecycle
- x86 cabinet IO compatibility
- x64 Unity compatibility
- runtime path redirection
- keyboard input
- Xbox/XInput input
- generic WinMM joystick input
- configurable control editor
- live control test
- Steering
- Dive/Climb
- Trigger/Confirm/Boost
- Start
- Secondary raw cabinet button
- Coin
- Service
- Test
- configurable Exit
- immediate user-requested session shutdown
- original operator menus
- original sound/settings pages
- standalone Quick Race
- normal UI monitor placement
- no-GUI cabinet launch
- no-GUI Exit binding
- XInput rumble
- native cabinet output observation
- localhost TCP output server
- localhost HTTP output monitor
- real two-PC cabinet linking
- linked attract synchronization
- multiplayer-ready linked state
- secondary-drive/runtime-with-spaces operation
- self-contained deployment
- guarded Shell startup-delay reduction

Secondary is verified as the original raw cabinet button. It is not established as the game's selection-menu Back action.

## B. Repository structure

Production source remains under `Development/loader-src/`.

Important paths:

| Path | Purpose |
|---|---|
| `Development/loader-src/DroneRacingGenesisLoader/DroneRacingGenesisLoader.csproj` | Main .NET 8 WinForms launcher |
| `Development/loader-src/DroneRacingGenesisLoader/Program.cs` | Main entry point, preflight and no-GUI startup |
| `Development/loader-src/DroneRacingGenesisLoader/Installation.cs` | Runtime-root discovery and required-component checks |
| `Development/loader-src/DroneRacingGenesisLoader/RuntimeConfiguration.cs` | Path, controls and network configuration generation |
| `Development/loader-src/DroneRacingGenesisLoader/ProcessMonitor.cs` | Shell/game ownership, lifecycle and shutdown |
| `Development/loader-src/DroneRacingGenesisLoader/PhysicalInputManager.cs` | Shared keyboard/XInput/WinMM input broker |
| `Development/loader-src/DroneRacingGenesisLoader/ControlsForm.cs` | Control editor |
| `Development/loader-src/DroneRacingGenesisLoader/ControlTestForm.cs` | Live control test and binding capture |
| `Development/loader-src/DroneRacingGenesisLoader/FeatureSettings.cs` | Network and output settings dialogs |
| `Development/loader-src/DroneRacingGenesisLoader/OutputManager.cs` | Native output consumer, rumble and TCP server |
| `Development/loader-src/DroneRacingGenesisLoader/OutputMonitorServer.cs` | Local HTTP output monitor |
| `Development/loader-src/DroneRacingGenesisLoader/DisplayManager.cs` | Windows display/window placement support |
| `Development/loader-src/DroneRacingGenesisLoader/ShellStartupArguments.cs` | Shell startup handoff and supported-build handling |
| `Development/loader-src/DroneRacingGenesisLoader/UnityPortability.cs` | Managed Unity startup preparation |
| `Development/loader-src/DroneRacingGenesis.Plugin/PortablePathMap.cs` | Managed path-map support |
| `Development/loader-src/DroneRacingGenesis.Plugin/ShellSecurityWorkaround.cs` | Original Shell security-setting preparation |
| `Development/loader-src/IOCompatibility/io_module.cpp` | Native x86 Shell IO, outputs, Shell patches and child adaptation |
| `Development/loader-src/IOCompatibility/bootstrap.cpp` | x86 suspended Shell bootstrap |
| `Development/loader-src/NativeUnity/unity.cpp` | x64 Unity path/input compatibility and native guards |
| `Development/loader-src/NativeUnity/injector.cpp` | x64 Unity bootstrap/injection |
| `Development/loader-src/NativeUnity/common.h` | Shared native utility/hash functions |
| `Development/loader-src/DroneRacingGenesisLoader.Tests/` | Generated-fixture regression tests |
| `Development/loader-src/DroneRacingGenesisLoader/Assets/` | Launcher logo/icon |
| `config/` | Sanitized release defaults |
| `docs/` | Runtime/build/acceptance documentation |
| `build.ps1` | Complete project build entry point |

Historical research captures, private runtimes, proprietary game data, temporary analysis output and local build products should remain outside Git.

## C. Fresh-clone build

From a Windows x64 development system:

```powershell
git clone https://github.com/StillJC/DroneLauncher.git
cd DroneLauncher
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Development requirements:

- .NET SDK 8.0.420 or compatible 8.0.4xx patch allowed by `global.json`
- Visual Studio C++ x86/x64 desktop tools
- Windows SDK
- normal NuGet access for first restore

Output:

```text
artifacts/Runtime/
```

Important binaries:

```text
DroneLauncher.exe                         x64
DroneRacingGenesis.IO.dll                x86
DroneRacingGenesis.IOBootstrap.exe       x86
DroneRacingGenesis.Unity.dll             x64
DroneRacingGenesis.UnityBootstrap.exe    x64
```

The launcher is self-contained .NET 8.

The native helpers use architecture-matched builds and static CRT linkage.

Original game files are not required to compile the repository or run the generated-fixture regression tests.

## D. Runtime layout

Expected merged runtime:

```text
Runtime/
  DroneLauncher.exe

  Launcher/
    Config/
    Logs/
    Plugins/
      IO/
      Unity/
    Licenses/
    README.md
    THIRD_PARTY_NOTICES.md

  Shell/
  ShellData/
  GameData/
  DroneRacing/
  backup/
```

Launcher-owned files should be merged into a copy of the user's supported original installation.

Preserve existing:

```text
Launcher/Config/
```

when upgrading an existing cabinet.

Do not add unrelated directories to Runtime root.

The original Shell scans Runtime root and can mistake arbitrary directories for game entries.

Direct layout and a nested `Sega` content layout are detected from the launcher executable location.

## E. Startup chain

Normal startup:

```text
DroneLauncher.exe
  |
  +-> DroneRacingGenesis.IOBootstrap.exe (x86)
        |
        +-> Shell.exe /k=<computed handoff>   [initially suspended]
              |
              +-> DroneRacingGenesis.IO.dll initialized
              |
              +-> Shell resumes
                    |
                    +-> DroneRacing.exe created by original Shell
                          [initially suspended]
                          |
                          +-> DroneRacingGenesis.UnityBootstrap.exe (x64)
                                |
                                +-> DroneRacingGenesis.Unity.dll initialized
                                      |
                                      +-> original Unity main thread resumes
```

The architecture-specific bootstraps initialize compatibility before the relevant original process continues.

The original Shell remains the owner of:

- normal game child creation
- operator lifecycle
- TEST/operator transitions
- cabinet state
- original network system

Drone Launcher does not replace the original game's installer/update system.

## F. Shell compatibility

Reference supported Shell SHA256:

```text
59BF7C7676A4AAEC584B4FAF81CDBAF36A6F9B267E69BC0D01250989053AF82B
```

`ShellStartupArguments.cs` supplies the required startup handoff.

`ProcessMonitor.cs` explicitly supplies the correct Shell working directory.

`RuntimeConfiguration.Generate` updates only understood runtime configuration fields and preserves unrelated original cabinet state.

The supplied original Shell/DK2 files are not rewritten on disk.

### Native x86 cabinet IO

`IOCompatibility/bootstrap.cpp` and `io_module.cpp` implement the known original MkII cabinet interface required by the supported Shell.

This includes the required original setup/read/write behavior and startup handshake.

Do not replace this with a second independent cabinet-input implementation.

### Shell system information

The compatibility layer provides narrow Shell information responses for the supported build, including the launcher identity shown by the original system-information page.

This is implemented through a targeted Shell IAT hook rather than changing the original executable on disk.

### Guarded in-memory Shell patches

The current Shell compatibility layer intentionally applies narrow, build-specific in-memory patches.

The original executable on disk is never modified.

Current startup optimizations include:

- original long game-verification wait bypass
- original two-second startup timer bypass
- original one-second state-20 delay bypass
- original five-second pre-launch timer bypass
- unused pre-launch checksum calculation bypass

Each code patch checks the expected original bytes before changing process memory.

If an expected signature does not match, that optimization is left untouched.

### Delays intentionally preserved

Do not blindly remove all remaining Shell waits.

The following were intentionally retained:

- state-28 network initialization timing
- state-0 cabinet/input readiness behavior

The network path is especially important now that real linked-cabinet operation has been verified.

## G. Unity compatibility

Reference supported GameAssembly SHA256:

```text
491F7C3E3AB392F78AEA8B3B9775B65AAD63ACDE2AE4E2FC4D04FDFE716FB652
```

The redundant managed whole-file GameAssembly SHA check was removed from `UnityPortability.cs`.

The native Unity compatibility layer still performs supported-build validation and narrow guards required by its implementation.

Do not casually remove native validation without understanding every address/signature it protects.

The current native layer provides narrow path and input compatibility for the original IL2CPP game.

Known redirected original data includes:

- ShellData
- GameSettings
- GameData Exists
- GameData Load
- GameData Save
- GameData Delete

There is no broad global filesystem hook.

The Unity main thread does not resume until required native initialization succeeds.

## H. Unity bootstrap performance

`NativeUnity/injector.cpp` performs the first main-module lookup as a single attempt because the newly created suspended child already has its primary image mapped.

Later module lookups retain retry behavior where appropriate.

This reduced the compatibility bootstrap phase from roughly:

```text
~610 ms
```

to approximately:

```text
~141 ms
```

Do not restore the initial unnecessary retry loop unless new evidence requires it.

## I. Remaining Unity startup time

The remaining noticeable game-start delay occurs inside the original Unity application before `RootScene.Awake()`.

Observed Player.log sequence:

```text
Unity engine initialization
D3D11 device creation
input initialization

approximately 10-second gap

RootScene/game initialization
```

IL2CPP analysis identified:

```text
RootScene.Awake()
RVA    0xDED180
Offset 0xDEBB80
VA     0x180DED180
```

The installation contains very large Unity resource files, including multi-gigabyte streamed resource data.

Storage speed can therefore have a material effect on startup and scene loading.

Do not assume this remaining delay belongs to Drone Launcher.

## J. Input architecture

`PhysicalInputManager` is the single physical-input implementation.

During a game session one broker samples physical input and publishes a shared mapped state used by both Shell and Unity compatibility.

Supported physical sources:

- keyboard
- XInput
- WinMM joystick

Keyboard input is scoped to launcher/Shell/game ownership rather than being treated as unrestricted global input.

XInput polls standard controller slots.

WinMM supports generic Windows joystick devices.

Generic WinMM IDs are enumeration indices, not permanent device identities.

### Shared mapping

Shell receives original-format cabinet bytes.

Unity receives normalized local values derived from the same broker.

Do not introduce a separate second Unity physical-input polling stack.

### Public control functions

Current public functions:

```text
Steering Left / Right
Vertical Up / Dive
Vertical Down / Climb
Trigger / Confirm / Boost
Start
Secondary
Coin
Service
Test
Exit
```

Steering and Vertical can use:

- one analog axis
- or two digital buttons/keys

Opposing digital directions cancel to neutral.

### Secondary

Secondary is confirmed as the original raw cabinet button.

It is not established as SelectFlow Back.

Do not document it as Back without new game-behavior evidence.

### Hidden legacy mappings

Old internal/legacy mappings may remain preserved for configuration compatibility even when they are not shown in the public UI.

Do not delete unknown/custom configuration entries merely because the current UI does not expose them.

## K. Control editor

The control editor is implemented in:

```text
ControlsForm.cs
ControlTestForm.cs
PhysicalInputManager.cs
```

Double-click capture replaces only the selected function.

Unmodified functions, hidden functions and custom/legacy assignments are preserved.

Control saving is atomic and retains the previous configuration.

Changes apply to the next game session.

Test Controls uses the same underlying input implementation as the real game session.

## L. Exit behavior

The configured Exit function is a launcher-level action.

In the normal UI, Exit can use the optional confirmation prompt.

The user can disable the prompt with:

```text
Don't ask again
```

and re-enable it through:

```text
Confirm before Exit
```

For an explicit Exit request, Drone Launcher now terminates its owned Shell/game process trees promptly.

The previous long approximately 25-second graceful-exit wait is not used for this deliberate user action.

Do not confuse this with the original TEST/operator lifecycle, which remains under the original Shell.

## M. No-GUI mode

Cabinet/front-end launch:

```text
DroneLauncher.exe -nogui
```

also accepts:

```text
DroneLauncher.exe --nogui
```

No-GUI mode:

- never shows the launcher form
- loads normal saved configuration
- generates the same runtime configuration
- starts the normal Shell/game chain
- starts the normal input broker
- keeps output handling active
- watches the configured Exit binding
- shuts down the session on Exit
- returns when the cabinet session ends

No-GUI mode intentionally uses the Windows primary display.

There is currently no separate monitor command-line option.

For dedicated cabinets, configure the desired cabinet screen as Windows primary.

## N. Monitor placement

Normal UI launches place owned Shell/game windows on the display containing the Drone Launcher window.

This allows a user to select the launch monitor simply by moving the launcher to that screen.

Implementation lives primarily in:

```text
DisplayManager.cs
ProcessMonitor.cs
```

The previous replacement graphics/settings feature was removed.

Drone Launcher 1.0 does not expose replacement controls for:

- resolution
- graphics quality
- Windowed/Borderless choice
- exclusive fullscreen

Rendering remains under the original game/Unity/Windows behavior.

`DisplayManager.cs` remains important for window placement even though the graphics-settings UI was removed.

## O. Original operator system

The original operator interface remains the supported method for cabinet settings.

The original operator handles:

- audio
- calibration
- coin/accounting
- network state
- input tests
- cabinet settings

The launcher no longer provides separate public buttons for:

- Operator / Test Menu
- Audio Settings
- Display / Graphics
- Diagnostics

Service and Test controls reach the original cabinet/operator implementation directly.

Do not recreate replacement operator pages unless there is a strong technical reason.

## P. Outputs

Original Shell cabinet output activity is observed in the x86 compatibility module and passed through the shared output ring to `OutputManager`.

Handshake traffic is excluded from public logical output reporting.

Verified logical output names:

```text
vibration
billboard
controller_red
controller_green
controller_blue
footwell_red
footwell_green
footwell_blue
monitor_lower_red
monitor_lower_green
monitor_lower_blue
```

Values are binary.

Unknown decorative patterns remain diagnostic-only.

The native producer must never block waiting for an external output consumer.

## Q. XInput rumble

Rumble follows the original cabinet vibration output.

Configuration selects:

- XInput controller slot
- rumble strength

Both XInput motors receive the same source level because the original game exposes one vibration state.

Do not synthesize effects from game inputs.

Generic joystick force feedback is not implemented.

Loss of the native output heartbeat/session shutdown must clear rumble.

## R. TCP output server

Current TCP behavior is server-based.

Drone Launcher listens on:

```text
127.0.0.1:<configured port>
```

External output software connects to Drone Launcher as a TCP client.

Protocol:

```text
UTF-8 newline-delimited JSON
```

Example:

```json
{"version":1,"sequence":12,"time":"...UTC...","output":"vibration","value":1}
```

Connection behavior:

1. client connects
2. launcher sends one current-state snapshot
3. launcher sends subsequent output transitions
4. disconnect does not block the game

There is no recurring one-second state refresh.

Do not revert documentation or code to the old outbound-client model unless the feature is deliberately redesigned.

This is a Drone Launcher-specific protocol.

Do not claim direct compatibility with:

- MAME Hooker
- OutputHooker
- other unrelated arcade-output protocols

without implementing an adapter.

## S. Local HTTP output monitor

`OutputMonitorServer.cs` provides a localhost-only monitor.

Human-readable page:

```text
http://127.0.0.1:8765/
```

JSON API:

```text
http://127.0.0.1:8765/api
```

The monitor is intended for:

- diagnostics
- integration development
- observing current logical output state

It is not intended to expose cabinet data to the LAN.

## T. Networking

Network UI and generation are primarily implemented in:

```text
FeatureSettings.cs
RuntimeConfiguration.cs
```

Original fields managed by the launcher:

```text
LinkPlay
CabinetID
NumCabinets
```

Standalone defaults to:

```text
LinkPlay=0
CabinetID=1
NumCabinets=1
```

LAN mode supports two to four cabinets.

Each cabinet must use a unique Cabinet ID.

LAN settings apply once and then preserve later original operator changes.

Do not continuously rewrite operator network configuration every launch.

No Windows Firewall rules are created or modified by Drone Launcher.

### Known original network services

Prior observation identified original services including:

```text
Shell local game TCP:      18888
Shell cabinet TCP:         27105
Unity host/discovery UDP:  8213
```

The existence of these sockets should not be treated as the sole proof of working networking.

Real two-peer testing has now been completed.

## U. Real linked-cabinet result

A live two-PC test successfully established the original cabinet link.

Test configuration:

```text
Cabinet 1
LinkPlay=1
CabinetID=1
NumCabinets=2

Cabinet 2
LinkPlay=1
CabinetID=2
NumCabinets=2
```

Network topology:

- both PCs on the same IPv4 subnet
- one connected through Ethernet
- one connected through Wi-Fi
- successful bidirectional IP connectivity before launch

Observed result:

- initial network state took time to settle
- link eventually established
- attract-mode state synchronized
- both cabinets displayed multiplayer-ready behavior

Attract playback was not necessarily frame-identical while synchronization settled.

That did not prevent successful link establishment.

## V. Storage observation

One linked test machine ran the game from USB storage.

That system showed corruption during one specific attract sequence while otherwise:

- linking successfully
- reaching multiplayer-ready state
- rendering other sequences normally

The machine itself was substantially more powerful than the other cabinet.

Because the original Unity installation streams very large assets, slow/removable media is a plausible factor.

No code change was made based on this observation.

Internal SSD/NVMe storage is the recommended deployment target.

## W. Configuration files

All paths below are relative to the detected content root.

Important launcher-owned configuration:

| Path | Purpose |
|---|---|
| `Launcher/Config/controls.json` | Input schema/bindings |
| `Launcher/Config/network.json` | Standalone/LAN selection and cabinet IDs |
| `Launcher/Config/outputs.json` | Rumble and local TCP server settings |
| `Launcher/Config/shell-compatibility.json` | Shell compatibility enablement |
| `Launcher/Config/unity-portability.json` | Unity compatibility enablement |
| `Launcher/Config/Original/*` | Runtime-created original backups |

The previous launcher graphics configuration is no longer part of the active 1.0 feature set.

If a stale `graphics.json` exists from an older development build, do not treat it as current public configuration.

Existing cabinet:

- audio
- calibration
- credits
- bookkeeping
- network operator state

must remain preserved unless the user explicitly changes the corresponding setting.

## X. UI source map

The launcher UI is constructed directly in C#.

There are no required WinForms Designer files to keep synchronized.

| UI area | Source |
|---|---|
| Entry point / no-GUI | `Program.cs` |
| Main launcher | `LoaderForm.cs` |
| Configure Controls | `ControlsForm.cs` |
| Test Controls / capture | `ControlTestForm.cs` |
| Network / LAN | `FeatureSettings.cs`, `RuntimeConfiguration.cs` |
| Outputs | `FeatureSettings.cs`, `OutputManager.cs` |
| Local output monitor | `OutputMonitorServer.cs` |
| Input broker / Exit presses | `PhysicalInputManager.cs` |
| Process lifecycle | `ProcessMonitor.cs` |
| Monitor/window placement | `DisplayManager.cs` |
| Shared launcher style | `ControlTestForm.cs` / `LauncherStyle` |
| Logo/icon | `Assets/DroneLauncher.png`, `Assets/DroneLauncher.ico` |

Removed public UI areas:

```text
Operator / Test Menu button
Audio Settings button
Display / Graphics button
Diagnostics button
```

Do not use old documentation referring to those controls as current behavior.

## Y. Branding

Public product name:

```text
Drone Launcher
```

Executable:

```text
DroneLauncher.exe
```

Current internal namespace remains:

```text
DroneRacingGenesisLoader
```

The internal namespace does not need to be renamed merely for branding.

Launcher logo/icon are embedded from:

```text
Development/loader-src/DroneRacingGenesisLoader/Assets/
```

Runtime must not depend on files in the Development tree.

Project-owned source and branding are MIT-licensed.

Third-party/runtime notices are documented in:

```text
THIRD_PARTY_NOTICES.md
```

## Z. Do not break

The following architectural rules should be treated as intentional.

### Runtime ownership

- Do not place arbitrary new folders at Runtime root.
- Launcher additions belong under `Launcher/`.
- Preserve user `Launcher/Config` during upgrades.

### Original binaries

- Do not patch original game/Shell executables on disk.
- Keep compatibility changes in memory.
- Preserve original manifests and CRC data.

### Shell lifecycle

- Shell remains the normal game-child owner.
- Shell remains the original TEST/operator owner.
- Unity compatibility must initialize before the suspended child resumes.

### Input

- Maintain one physical-input broker.
- Do not create a second Unity controller polling implementation.
- Do not write into unrelated network input arrays.
- Preserve unknown/custom/hidden control bindings.

### Path compatibility

- Keep redirects narrow and call-site specific.
- Do not replace them with a broad global filesystem redirect without strong justification.

### Networking

- Preserve original network initialization timing unless evidence proves it safe to change.
- Preserve operator-edited LAN state after launcher apply-once configuration.
- Do not automatically modify Windows Firewall.

### Outputs

- Output observation must remain nonblocking.
- Rumble must clear on shutdown/lost heartbeat.
- External output clients must never be required for gameplay.
- TCP server remains localhost-only unless intentionally redesigned.

### Exit behavior

- Explicit launcher Exit should remain prompt/immediate according to saved user preference.
- Do not confuse this with TEST/operator lifecycle.

### Deployment

- Keep the .NET application self-contained.
- Keep architecture-matched native helpers.
- Do not add a requirement for Python, Frida or development tools at runtime.

## Remaining work

The 1.0 feature set is suitable for release.

Future work should be limited to concrete issues or verified user reports rather than speculative redesign.

## Release reference

Primary release documentation:

```text
README.md
docs/RUNTIME.md
docs/FEATURE_STATUS.md
docs/ACCEPTANCE.md
docs/BUILD.md
docs/RELEASE_OUTPUTS.md
THIRD_PARTY_NOTICES.md
LICENSE
```

The repository should remain sufficient for a fresh developer to:

1. clone the source
2. build the launcher
3. understand the supported runtime layout
4. merge the launcher with their own supported original installation
5. configure controls/network/outputs
6. launch normally or with `-nogui`
7. continue development without relying on private historical research files