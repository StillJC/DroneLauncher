# Acceptance

Drone Launcher 1.0 has been validated against the supported Drone Racing Genesis installation on real Windows systems.

The purpose of this document is to record what has actually been exercised and what remains outside the current acceptance scope.

## Build and packaging

The project builds successfully from source using the documented .NET and Visual C++ toolchain.

The release build produces:

```text
DroneLauncher.exe                         x64
DroneRacingGenesis.IO.dll                x86
DroneRacingGenesis.IOBootstrap.exe       x86
DroneRacingGenesis.Unity.dll             x64
DroneRacingGenesis.UnityBootstrap.exe    x64
```

The launcher is published as a self-contained .NET 8 application.

Users do not need a separate .NET installation.

The built runtime contains launcher-owned files only. Original game content is not included.

Original Shell and game binaries remain unchanged on disk.

## Supported game build

Known reference Shell SHA256:

```text
59BF7C7676A4AAEC584B4FAF81CDBAF36A6F9B267E69BC0D01250989053AF82B
```

Known reference GameAssembly SHA256:

```text
491F7C3E3AB392F78AEA8B3B9775B65AAD63ACDE2AE4E2FC4D04FDFE716FB652
```

The supported target is the known original game build.

Whole-file hashes identify the reference build, while native compatibility additionally relies on narrow signature/build checks around required runtime hooks and patches.

## Original-file integrity

Drone Launcher does not rewrite the original Shell or Unity game executables.

Compatibility is implemented through:

- architecture-matched bootstrap helpers
- targeted API hooks
- guarded in-memory byte patches
- narrow path redirection
- local input translation
- local output observation
- process lifecycle management

Original CRC/update files remain untouched.

Runtime auditing has verified original manifest integrity and protected-file identity against an untouched source installation.

The included audit tool can be used against user-owned source/runtime copies:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\AuditRuntime.ps1 -Root '<working runtime>' -OriginalRoot '<untouched content root>' -Output '<local report.json>'
```

The audit tool reads and compares files only.

## Startup and Shell compatibility

The original Shell starts successfully through Drone Launcher using the required working directory and startup handoff.

Verified behavior includes:

- correct Shell startup
- x86 IO compatibility initialization
- original cabinet handshake
- original operator states
- Unity child creation
- repeated Unity child relaunches under the same Shell

Several original startup waits that are unnecessary in this environment are bypassed through exact guarded in-memory patches.

Verified startup optimizations include:

- long original game-verification wait
- two-second Shell startup timer
- one-second Shell state delay
- five-second pre-launch timer
- unused pre-launch checksum calculation

The original network initialization delay is intentionally preserved.

Original cabinet/input readiness logic is also intentionally preserved.

No original Shell executable is modified on disk.

## Unity compatibility

The original Unity game starts successfully through the x64 compatibility bootstrap.

Verified behavior includes:

- suspended child creation
- native compatibility initialization before main-thread resume
- required path redirection
- mapped cabinet input
- original game startup
- repeated fresh Unity child initialization

The managed launcher no longer broadly rejects a modified `GameAssembly.dll` solely because its whole-file SHA differs.

Native compatibility still requires the expected narrow signatures for the supported build.

## Startup performance

The compatibility layer substantially reduces original Shell startup delays.

The remaining Unity startup time is primarily within the original game before `RootScene.Awake()` and is consistent with Unity asset loading/parsing.

The original installation contains very large resource files, including multi-gigabyte asset data.

Storage performance can therefore materially affect startup and scene loading.

Internal SSD/NVMe storage is recommended.

## Standalone gameplay

Standalone gameplay has been exercised through the original Shell/game lifecycle.

Verified sequence includes:

- attract mode
- coin input
- start input
- Quick Race
- drone/course selection
- race start
- steering
- Dive/Climb
- Boost
- race completion
- Game Over
- return to attract

The game successfully returns to the normal cabinet lifecycle afterward.

## Controls

Keyboard controls have been exercised in the original operator menus and gameplay.

Verified keyboard functions include:

```text
Steering Left / Right
Dive
Climb
Start
Trigger / Confirm / Boost
Secondary
Coin
Service
Test
Exit
```

Physical Xbox controller input has been exercised.

Verified XInput behavior includes:

- steering axis
- vertical axis
- buttons
- Trigger/Confirm
- Boost
- Start
- Service/Test-related control use

A separate generic Windows joystick has also been tested through WinMM.

Verified generic joystick behavior includes:

- normalized axis range
- face-button mappings
- neutral return on release

Generic WinMM joystick IDs remain enumeration-based and can change between Windows/device configurations.

## Control editor

The control editor has been exercised for:

- double-click capture
- keyboard input
- controller buttons
- controller axes
- two-button axis mapping
- cancellation
- opposite-direction neutral behavior
- persistence
- preservation of untouched/custom/hidden bindings

The editor saves only the intended control changes.

## Original operator system

The original operator system remains active and usable.

Verified behavior includes:

- Service navigation
- Test selection
- original input test
- original sound settings
- original network settings
- operator exit back to game
- repeated operator/game cycles

Audio, calibration, bookkeeping, coin configuration, and other cabinet settings remain owned by the original operator software.

Drone Launcher does not replace these pages.

## Exit handling

The configured Exit function has been tested.

Verified behavior includes:

- keyboard/controller Exit binding
- confirmation prompt
- Cancel
- Don't ask again
- prompt re-enable
- immediate shutdown of launcher-owned Shell/game processes

Explicit Exit no longer waits through the older long graceful-shutdown path.

This is intentional user-requested session termination behavior.

## No-GUI mode

Drone Launcher supports cabinet/front-end launch through:

```text
DroneLauncher.exe -nogui
```

and:

```text
DroneLauncher.exe --nogui
```

Verified no-GUI behavior includes:

- no launcher window displayed
- saved configuration loaded
- runtime configuration generated
- Shell launched normally
- Unity launched normally
- input broker remains active
- output handling remains active
- configured Exit binding works
- session closes correctly

No-GUI mode uses the Windows primary display.

## Monitor placement

Normal UI launches place the Shell and game on the Windows display containing the Drone Launcher window.

Verified behavior includes:

- primary-display launch
- secondary-display launch
- repeated launches
- correct child placement

No-GUI mode intentionally uses the Windows primary display.

Drone Launcher 1.0 does not provide replacement graphics/quality controls.

## Network and linked cabinets

Standalone networking is verified.

Real two-machine linked-cabinet operation has also been tested.

A two-cabinet configuration was used:

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

The two test systems were on the same subnet.

One system used Ethernet and the other used Wi-Fi.

Verified linked behavior includes:

- both machines could communicate on the LAN
- both received the intended original network settings
- link establishment completed
- attract behavior synchronized after the link settled
- both cabinets displayed multiplayer readiness

The attract sequences were not always frame-identical during initial synchronization.

That did not prevent successful linked multiplayer-ready state.

A complete linked multiplayer race has not yet been exercised.

## Outputs

The original cabinet output stream has been observed successfully.

Verified logical outputs include:

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

Values are binary states.

Some decorative cabinet patterns remain diagnostic-only.

## XInput rumble

Physical XInput rumble has been verified.

The original cabinet vibration state drives the selected XInput controller.

Both XInput motors receive the same level because the original game provides one vibration state.

Generic joystick force feedback is not implemented.

## TCP output server

The TCP output system has been exercised as a localhost server.

Drone Launcher listens on:

```text
127.0.0.1:<configured port>
```

External software connects to Drone Launcher.

The protocol is UTF-8 newline-delimited JSON.

Example:

```json
{"version":1,"sequence":12,"time":"2026-10-03T16:00:00.0000000+00:00","output":"vibration","value":1}
```

Verified behavior includes:

- client connection
- current-state snapshot on connect
- transition messages after connection
- disconnect/reconnect
- game continues without a client
- no recurring one-second state refresh

This is a Drone Launcher-specific protocol.

Direct MAME Hooker or OutputHooker compatibility is not claimed.

## Local output monitor

The local HTTP output monitor has been exercised.

Human-readable endpoint:

```text
http://127.0.0.1:8765/
```

JSON endpoint:

```text
http://127.0.0.1:8765/api
```

The monitor is localhost-only.

## Secondary-drive and path testing

The launcher has been exercised from a non-system drive and from paths containing spaces.

Long-path testing within normal Windows path behavior has also succeeded.

Original Shell limitations remain.

The following are not claimed:

- arbitrary Unicode path support
- arbitrary extended-length Windows path support

## Removable-storage observation

A second linked-cabinet test system was run from USB storage.

The link itself functioned correctly.

One specific attract sequence showed graphics corruption on that machine only.

Because the system was otherwise more powerful and the issue was isolated to one streamed sequence while running from removable storage, storage performance remains a plausible cause.

No LAN or launcher defect was demonstrated by that observation.

Internal SSD/NVMe storage is recommended for normal deployment.

## Self-contained deployment

The built runtime is self-contained.

Verified deployment behavior includes:

- launcher copied independently of the source tree
- no Python requirement
- no Frida requirement
- no separate .NET installation requirement
- x86/x64 native helpers included
- original game content supplied separately
- existing launcher configuration can be preserved during upgrades

## Accepted 1.0 functionality

The following functionality is accepted for Drone Launcher 1.0:

```text
Self-contained Windows launcher
Original Shell startup
Original game startup
x86 cabinet IO compatibility
x64 Unity compatibility
Path redirection
Keyboard controls
Xbox controls
Generic WinMM joystick controls
Control configuration UI
Control test UI
Original operator menus
Standalone gameplay
Configured Exit handling
No-GUI launch
No-GUI Exit handling
Normal UI monitor placement
Linked-cabinet configuration
Real two-PC link discovery
Linked attract synchronization
Multiplayer-ready state
Original cabinet output observation
XInput rumble
Local TCP output server
Local HTTP output monitor
Repeated Shell/game lifecycle
Startup-delay reduction
```
