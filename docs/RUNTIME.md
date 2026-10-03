# Drone Launcher Runtime

Drone Launcher is a self-contained Windows x64 launcher and native compatibility layer for **Drone Racing Genesis**.

Start `DroneLauncher.exe`, configure controls if needed, then select **Launch Game**.

Python, Frida, a separate .NET installation, and the Development tree are not required at runtime.

The launcher starts the original Shell with the required handoff and working directory, initializes the x86 cabinet-IO compatibility layer, and then allows the original Shell to start the Unity game. The Unity child is initialized through a matching x64 compatibility bootstrap before its main thread resumes.

Original Shell and game binaries remain unchanged on disk. Compatibility is implemented through architecture-matched helper processes, targeted hooks, guarded in-memory patches, narrow path redirection, and local input/output adapters.

## Controls

**Configure Controls** shows all configurable functions on one page.

Double-click a row, or press Enter while it is selected, to capture a keyboard key, XInput button/axis, or Windows joystick button/axis.

Capturing replaces only the selected function's assignments. Untouched, hidden, custom, and legacy bindings are preserved.

For Steering or Vertical:

- move a physical axis in the prompted direction
- or press the first direction key/button and then the opposite direction key/button

Cancel preserves the previous assignment.

Deadzone, sensitivity, and direction reversal are configurable.

Changes apply to the next game session. The previous control configuration is retained beside `controls.json`.

Flight-style controls use:

- Steering Left / Right
- Vertical Up = Dive
- Vertical Down = Climb

A flight axis can also use two digital keys/buttons. Opposite directions cancel to neutral.

The original trigger combines **Confirm and Boost**.

Secondary reaches the original cabinet button but is not a proven Back action in the game's selection menus.

Shipped keyboard defaults:

```text
Arrow Left / Right   Steering
Arrow Up             Dive
Arrow Down           Climb
Enter                Start
Z                    Trigger / Confirm / Boost
X                    Secondary
5                    Coin
F1                   Service
F2                   Test
Escape               Exit
```

Xbox mappings are also supplied.

Generic joystick input is supported through Windows WinMM.

## Test Controls

**Test Controls** uses the same physical-input sampler as the game.

When opened while no game is running, it previews the currently edited control assignments.

When opened during a running session, it shows the active configuration and the same physical inputs continue reaching the cabinet.

Device diagnostics may show connected hardware even before it has been assigned to a game function.

## Original operator system

The original operator system remains responsible for:

- audio settings
- calibration
- coin settings
- bookkeeping
- network cabinet state
- other original operator options

In original menus:

- Service chooses an item
- Test activates the selected item

The launcher does not replace the original operator UI.

The original game automatically sets local-race acceleration and braking behavior as expected by the cabinet software.

## Exit behavior

Double-click the Exit function in Configure Controls to assign one keyboard key or controller button.

A configured Exit press can show an Exit / Cancel confirmation.

Selecting **Don't ask again** and choosing Exit stores immediate-exit behavior for later sessions.

The confirmation prompt can be re-enabled with **Confirm before Exit**.

During an active game session, Exit immediately terminates the launcher-owned Shell/game process trees rather than waiting through the old long graceful-shutdown path.

The configured Exit binding also works in `-nogui` mode.

## No-GUI / front-end launch

For cabinet front-ends and automated startup:

```text
DroneLauncher.exe -nogui
```

`--nogui` is also accepted.

No-GUI mode:

- does not display the launcher UI
- loads the same saved configuration
- generates the normal runtime configuration
- starts the original Shell/game
- keeps input/output handling active
- honors the configured Exit binding
- exits after the cabinet session ends

No-GUI mode uses the Windows primary display.

This is the intended behavior for a dedicated cabinet where the front-end launches Drone Launcher directly.

## Monitor behavior

When launching from the normal UI, the Shell and Unity game are placed on the Windows monitor containing the launcher window.

This allows a user to move Drone Launcher to the desired display before launching.

No-GUI mode uses the Windows primary display.

Drone Launcher does not currently provide replacement graphics, quality, fullscreen, or resolution controls.

The game's normal Unity/Windows rendering behavior remains in use.

## Startup compatibility and performance

Several original Shell delays and verification stages are unnecessary when the game is launched through Drone Launcher.

For the supported Shell build, the x86 compatibility module uses guarded in-memory patches to reduce startup time while leaving the original executable unchanged on disk.

Current startup optimizations include:

- bypassing the original long game-verification wait while continuing through Shell success cleanup
- bypassing the original pre-launch timer delay
- bypassing the original two-second Shell startup delay
- bypassing the original one-second state-20 delay
- skipping an unused pre-launch checksum calculation

These are narrow, build-specific runtime patches.

The original network initialization delay is intentionally retained.

The original cabinet/input-readiness startup logic is also intentionally retained.

The remaining Unity startup time is largely controlled by the original game itself.

The installation contains multi-gigabyte Unity resource files, including a `resources.assets.resS` file over 3 GB, so storage performance can have a noticeable effect on startup and scene loading.

An internal SSD/NVMe drive is recommended over a slow USB flash drive or mechanical hard disk when possible.

## Network / linked cabinets

Standalone is the default network mode.

The launcher also supports the original linked-cabinet configuration.

For two cabinets:

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

Each cabinet must use a unique ID.

The configuration UI supports two to four cabinets.

Drone Launcher writes only the original network fields used by the game:

```text
LinkPlay
CabinetID
NumCabinets
```

LAN values are applied once. Later original operator network changes are preserved rather than overwritten every launch.

No Windows Firewall rules are created or modified.

Two real PCs on the same local network have been successfully linked.

Observed test result:

- Cabinet 1 used ID 1
- Cabinet 2 used ID 2
- both used NumCabinets=2
- both machines discovered the link
- attract-mode behavior synchronized after the link settled
- both systems displayed multiplayer readiness

A complete linked multiplayer race has not yet been tested.

The link may take a short period to settle after both cabinets start.

The two test systems successfully linked with one machine on Ethernet and the other on Wi-Fi on the same subnet.

## Outputs

Optional outputs are configured through the launcher Outputs page.

Supported output handling includes:

- XInput controller rumble
- vibration state
- billboard state
- controller red/green/blue
- footwell red/green/blue
- lower-monitor red/green/blue

Verified logical names:

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

Values are binary `0` or `1`.

These names represent observed original cabinet output levels. They do not imply analog brightness or additional inferred colors.

Some decorative cabinet patterns remain diagnostic-only.

## XInput rumble

XInput rumble follows the original cabinet vibration output.

A controller slot and rumble strength can be selected.

Both XInput motors receive the same level because the original game exposes one vibration state.

Generic joystick force feedback is not implemented.

Rumble is cleared when the native output heartbeat is lost or the game session ends.

## TCP output server

Drone Launcher can expose output events through a local TCP server.

The launcher listens on:

```text
127.0.0.1:<configured port>
```

External software connects to Drone Launcher as a TCP client.

The protocol is UTF-8 newline-delimited JSON.

Example:

```json
{"version":1,"sequence":12,"time":"2026-10-03T16:00:00.0000000+00:00","output":"vibration","value":1}
```

When a client connects, it receives one snapshot of the current output state.

After that, only output transitions are sent.

There is no recurring one-second refresh.

The connection is local-only and does not expose the server to the LAN.

This protocol is specific to Drone Launcher and should not be assumed to be directly compatible with MAME Hooker, OutputHooker, or other unrelated output systems.

## Local output monitor

A local HTTP monitor is available while the output system is active.

Human-readable view:

```text
http://127.0.0.1:8765/
```

JSON API:

```text
http://127.0.0.1:8765/api
```

The monitor is bound to localhost only.

## Installation layout

Copy the complete built Runtime payload into a copy of the supported original game installation.

Expected layout:

```text
Runtime/
  DroneLauncher.exe
  Launcher/
    Config/
    Logs/
    Plugins/
  Shell/
  ShellData/
  GameData/
  DroneRacing/
  backup/
```

Direct layout and a nested `Sega` content layout are detected from the launcher executable location.

Do not add arbitrary directories at Runtime root. The original Shell scans that location and can mistake unrelated directories for games.

Preserve the original `backup` directory and installation metadata.

Preserve existing `Launcher\Config` when updating the launcher.

## Configuration files

Launcher-owned configuration is stored under:

```text
Launcher\Config\
```

Important files include:

```text
controls.json
network.json
outputs.json
shell-compatibility.json
unity-portability.json
```

`Config\Original` contains runtime-created backups used to preserve original configuration state.

Working `ShellData` and `GameData` remain live original cabinet/game state.

The old launcher graphics configuration is no longer used.

Historical `network-observation.json` is not a production runtime requirement.

## Diagnostics

Useful runtime diagnostics include:

```text
Launcher\Logs\Loader\loader-*.log
Launcher\Logs\Loader\io-*.log
Launcher\Logs\Loader\unity-*.log
diagnostics.json
shell-lifecycle.json
unity-*.json
```

The native bootstrap processes are short-lived and exit after initialization.

The runtime compatibility modules remain active inside the Shell/game processes where required.

## Supported build

Known supported Shell SHA256:

```text
59BF7C7676A4AAEC584B4FAF81CDBAF36A6F9B267E69BC0D01250989053AF82B
```

Known supported GameAssembly SHA256:

```text
491F7C3E3AB392F78AEA8B3B9775B65AAD63ACDE2AE4E2FC4D04FDFE716FB652
```

The runtime uses additional narrow signature checks around native patch/hook locations.

Modified game files are not broadly rejected solely because their whole-file hash differs where launcher-side validation has been relaxed, but the supported target remains the known original build and required native signatures must still match.

Original game/Shell files are never rewritten by Drone Launcher.

## Path limitations

The original Shell uses legacy ANSI APIs and fixed-size buffers.

Because of those original limitations:

- arbitrary Unicode paths are not a supported claim
- extended-length Windows paths are not a supported claim
- unusual parent-directory names may still expose original Shell scanner behavior

Avoid unnecessary complexity in the installation path.

## Preflight

Development and troubleshooting can use:

```text
DroneLauncher.exe --preflight
```

Preflight verifies required component presence and generates the normal working configuration without launching Shell.

It is not a complete gameplay/runtime test.

## Build development

Production source is under:

```text
Development\loader-src\
```

Build from the repository root with:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

See `docs/BUILD.md` for prerequisites, toolchain information, and clean-build verification.

The generated runtime contains launcher-owned files only.

Original game files must be supplied separately.

Historical research scripts, proprietary runtime data, captures, local test installations, and build artifacts are intentionally excluded from Git.