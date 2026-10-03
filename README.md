# Drone Launcher

A Windows launcher and native compatibility layer for the supported **Drone Racing Genesis** installation. It preserves the original Shell/game lifecycle and operator system while relocating required runtime paths, providing configurable keyboard/controller input, supporting original linked-cabinet networking, and adapting the original cabinet IO for standard Windows hardware.

**Original game files are not included.** Supply your own legally obtained complete installation. This repository builds without those files; running the game requires them. It is an independent project and is not an official Sega distribution.

## Build

On Windows x64, install .NET SDK **8.0.420** (or a later 8.0.4xx patch allowed by `global.json`) and Visual Studio C++ x86/x64 tools plus a Windows SDK. The tested native toolchain is documented in [Build instructions](docs/BUILD.md).

```powershell
git clone https://github.com/StillJC/DroneLauncher.git
cd DroneLauncher
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output: `artifacts/Runtime/`, containing the self-contained .NET 8 x64 `DroneLauncher.exe`, x86/x64 native helpers, sanitized configuration and notices.

Users do not need .NET installed. Build prerequisites are development-only. See [release output map](docs/RELEASE_OUTPUTS.md).

## Runtime setup

Merge the launcher output with a **copy** of your matching original installation:

```text
Runtime/
  DroneLauncher.exe             # built here
  Launcher/                     # built/configured here
  Shell/                        # user-supplied game files
  ShellData/                    # user-supplied, mutable operator state
  GameData/                     # user-supplied, mutable scores/state
  DroneRacing/                  # user-supplied Unity game
  backup/                       # user-supplied original metadata
```

Preserve the existing `Launcher/Config` directory when upgrading.

Do not add arbitrary folders at Runtime root: the original Shell scans that location for games. Keep development/build material elsewhere. Direct and nested `Sega` content layouts are detected from the launcher executable location.

Start **DroneLauncher.exe**, configure controls if necessary, then select **Launch Game**.

Double-click a function in **Configure Controls** to capture a keyboard key, controller button or axis.

Shipped keyboard defaults include:

- Arrow keys: Steering and Dive/Climb
- Enter: Start
- Z: Trigger / Confirm / Boost
- X: Secondary
- 5: Coin
- F1: Service
- F2: Test
- Escape: Exit

Xbox mappings are also supplied. Generic Windows joystick input is supported through WinMM.

In the original operator menus, Service chooses an item and Test activates it. Audio, calibration, bookkeeping and other cabinet settings remain owned by the original operator system.

## Command-line launch

For cabinet/front-end use, Drone Launcher can start directly without displaying its UI:

```text
DroneLauncher.exe -nogui
```

`--nogui` is also accepted.

No-GUI mode:

- loads the same saved launcher configuration
- generates the normal runtime configuration
- starts and monitors the original Shell/game
- keeps input and output handling active
- honors the configured Exit binding
- exits after the cabinet session ends

No-GUI launches use the Windows primary display, which is the intended behavior for a normal dedicated cabinet setup.

## Network / linked cabinets

The **Network / LAN** page supports the original linked-cabinet configuration.

For a two-cabinet setup:

```text
Cabinet 1
  Network mode: LAN / Linked Cabinets
  Cabinet ID: 1
  Number of cabinets: 2

Cabinet 2
  Network mode: LAN / Linked Cabinets
  Cabinet ID: 2
  Number of cabinets: 2
```

Two real PCs on the same LAN have been successfully linked.

Testing confirmed:

- both cabinets discovered the link
- attract sequences synchronized after link establishment
- both cabinets reported multiplayer readiness

A complete linked multiplayer race has not yet been tested.

Each cabinet must use a unique Cabinet ID. Two to four cabinets are supported by the configuration UI.

LAN settings are applied to the original `ShellData.ini` network fields and then left under original operator control. Later operator network changes are preserved rather than continuously overwritten by the launcher.

Drone Launcher does **not** modify Windows Firewall rules. Both systems must be able to communicate with one another on the local network.

## Outputs

Optional cabinet outputs are available from the **Outputs** page.

Supported output handling includes:

- XInput controller rumble
- original vibration state
- billboard state
- controller RGB states
- footwell RGB states
- lower-monitor RGB states

Rumble follows the original cabinet vibration output. Both XInput motors receive the same level because the original cabinet exposes a single vibration state.

### TCP output server

Drone Launcher can expose output state through a local TCP server.

The server listens on:

```text
127.0.0.1:<configured port>
```

Clients connect to Drone Launcher. The protocol is UTF-8 newline-delimited JSON.

Example:

```json
{"version":1,"sequence":12,"time":"2026-10-03T16:00:00.0000000+00:00","output":"vibration","value":1}
```

A client receives a current-state snapshot when it connects, followed by output state transitions.

This is a Drone Launcher output protocol and should not be assumed to be directly compatible with MAME Hooker, OutputHooker or other unrelated output protocols.

### Local output monitor

A local diagnostic/output monitor is also available while the output system is active:

```text
http://127.0.0.1:8765/
```

JSON state is available at:

```text
http://127.0.0.1:8765/api
```

These interfaces listen only on localhost.

## Supported build and compatibility

Current supported game build:

- Unity **2019.4.8f1 x64 IL2CPP**
- Known Shell SHA256: `59BF7C7676A4AAEC584B4FAF81CDBAF36A6F9B267E69BC0D01250989053AF82B`
- Known GameAssembly SHA256: `491F7C3E3AB392F78AEA8B3B9775B65AAD63ACDE2AE4E2FC4D04FDFE716FB652`

Original game and Shell executables remain unchanged **on disk**.

Runtime compatibility is implemented through architecture-matched native helpers, targeted API hooks, exact/signature-guarded patches, and narrowly scoped in-memory compatibility changes for the supported build.

The launcher includes startup compatibility optimizations for original Shell delays and verification stages that are unnecessary in this environment. These changes occur only in process memory; the original files are not rewritten.

Standalone gameplay, original operator flow, keyboard controls, Xbox controls, a generic joystick, XInput rumble, TCP outputs, no-GUI launching and two-machine cabinet linking have all been exercised on real systems.

## Display behavior

Normal UI launches place the Shell/game on the display containing the launcher window.

No-GUI launches use the Windows primary display.

The launcher does not currently provide replacement graphics or quality controls. Rendering settings remain under the original game, Unity and Windows behavior.

## Startup performance

Several original Shell startup delays are bypassed through guarded in-memory compatibility patches.

The remaining game startup time can be dominated by the original Unity asset load. The game contains multi-gigabyte Unity resource files, so storage performance can materially affect startup and scene loading.

Running the game from an internal SSD or NVMe drive is recommended over a slow USB flash drive or mechanical disk when possible.

## Current limitations

Known limitations include:

- only the supported original build/signatures are a supported target
- original Shell ANSI/path-scanner restrictions remain
- arbitrary Unicode and extended-length Windows paths are not supported claims
- generic joystick device indices may change between Windows/device configurations
- generic joystick force feedback is not implemented

## Development documentation

Start development with [HANDOFF.md](HANDOFF.md).

Additional documentation:

- [Feature status](docs/FEATURE_STATUS.md)
- [Runtime usage](docs/RUNTIME.md)
- [Build verification](docs/BUILD.md)
- [Repository audit](docs/REPOSITORY_AUDIT.md)
- [Acceptance results](docs/ACCEPTANCE.md)
- [Release output map](docs/RELEASE_OUTPUTS.md)

## Licensing

Project source and owner-created launcher branding use the [MIT License](LICENSE).

See [third-party and distribution audit](THIRD_PARTY_NOTICES.md).

No proprietary game payload is part of this repository.