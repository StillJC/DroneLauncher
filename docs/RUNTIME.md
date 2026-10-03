# Drone Launcher

Start `DroneLauncher.exe`, then **Launch Game**. Drone Launcher is a self-contained Windows x64 application. Python, Frida, a separate .NET installation, and the Development tree are not required to run this version.

The loader starts the original Shell with its required handoff and working directory. Shell performs its original verification and starts Unity. Short-lived native bootstraps initialize cabinet IO and eight narrow Unity path redirects plus the local input adapter. Original game and Shell binaries and CRC/update manifests remain unchanged on disk.

## Controls and operator settings

- **Configure Controls** shows all functions on one page, one row per function. Double-click a row (or press Enter) to immediately listen for a keyboard key, XInput button/axis, or Windows joystick button/axis. Capturing replaces only that function's assignments; untouched and hidden/custom bindings remain intact. For Steering or Vertical, move the axis Left or Up/Dive as prompted, or press the first direction key/button and then the opposite direction key/button. Cancel keeps the previous assignment. Deadzone, sensitivity, and Reverse directions are on the same page. Save applies next launch and retains the previous configuration beside controls.json.
- Flight axes can use a real axis or **two direction buttons**. Left and Up/Dive are negative; Right and Down/Climb are positive. Opposite buttons cancel to neutral. Invert, deadzone and sensitivity remain configurable.
- **Test Controls** uses the same sampler as the game. When opened from Configure Controls while no game is running, it previews the edited assignments. With a running game, it shows active settings and inputs also reach the cabinet. Device diagnostics can show a connected controller even before it has been assigned to game actions.
- The original trigger combines **Confirm and Boost**. Secondary reaches the original cabinet button but does not mean Back in selection menus.
- **Operator / Test Menu** sends cabinet TEST, waits for Unity to exit, and focuses the original menu. If already in the menu, it only focuses it.
- **Audio Settings** explains how to select SOUND SETTINGS in the original operator menu.
- In original menus, Service chooses and Test selects. Shipped keyboard defaults: F1 Service, F2 Test, Enter Start, Z Confirm/Boost, X Secondary, arrows directions, 5 Coin and Escape Exit. Existing custom or legacy bindings are retained when upgrading.
- The original local race automatically sets acceleration to 1 and brake to 0. View currently invokes the original boost-related behavior; camera switching has not been established. Original selection Update consumes horizontal and Decide/Start, not Secondary or vertical menu navigation.

Audio, calibration, coin settings and bookkeeping belong to the operator. Launch generation changes only established path fields and standalone network fields; it does not restore factory audio every time.

## Exit, network and optional outputs

Double-click Exit Game / Launcher in Configure Controls to assign Exit to one keyboard key or controller button. A single press opens Exit / Cancel confirmation. Checking **Don't ask again** and choosing Exit persists immediate exit for future presses. Re-enable the prompt with **Confirm before Exit**. Exit uses the existing orderly shutdown sequence; force termination is a bounded failure fallback.

Standalone is the default network mode. LAN uses the original cabinet-link settings: unique cabinet IDs, two to four cabinets. Launcher LAN values apply once; later operator edits are preserved. No Windows Firewall rules are changed. Real linked-cabinet gameplay requires a separate peer and remains unverified in this pass.

Outputs are optional and disabled by default. XInput rumble follows original cabinet vibration events, with selectable controller and strength. Both motors receive the same level because the original output supplies one vibration state. Generic joystick force feedback is not implemented. TCP sends logical output states to a configured destination; receiver absence does not block the game.

TCP protocol: outbound UTF-8 newline-delimited JSON. Example:

```json
{"version":1,"sequence":12,"time":"2026-10-02T23:00:00.0000000+00:00","output":"vibration","value":1}
```

Verified names: `vibration`, `billboard`, `controller_red`, `controller_green`, `controller_blue`, `footwell_red`, `footwell_green`, `footwell_blue`, `monitor_lower_red`, `monitor_lower_green`, `monitor_lower_blue`. Values are binary 0/1. These describe observed original output levels, not inferred colors or analog brightness. Unknown decorative patterns stay in diagnostics.

Changed states are sent immediately, with a one-second state refresh. Connections start with current states. The queue holds 64 lines, drops oldest entries under pressure, and uses one-second connect/write deadlines with a two-second reconnect delay. It is a state stream, not a guaranteed pulse-delivery protocol. Sequence numbers are session-local; reconnect can skip sequence numbers. Incoming receiver bytes have no command meaning. A lost native heartbeat clears output states and stops rumble.

**Display / Graphics** supports tested Borderless and Windowed modes, Windows monitor selection, supported resolutions, and the project's six quality presets: Very Low, Low, Medium, High, Very High and Ultra. Original game default is Very High. Exclusive requests fell back to borderless in this build and are not offered. A missing selected display falls back to primary with a warning. Preferences reside in `Launcher\Config\graphics.json`; original game assets are never edited to save them.

## Installation and diagnostics

Copy the complete Runtime directory. Direct layout and a `Sega` content subdirectory are detected from the loader EXE. Logs stay under `Launcher\Logs\Loader`; do not add arbitrary directories at Runtime root because the original Shell scanner can mistake them for games. Preserve `backup` and original install manifests.

Shell uses legacy ANSI APIs and fixed-size buffers. Avoid a parent directory containing the word `shell`. Unicode and paths beyond legacy Windows limits are not supported claims. See the repository `HANDOFF.md` and `docs/FEATURE_STATUS.md` for tested cases and remaining checks.

`Launcher\Config\unity-portability.json` selects the native engine. `shell-compatibility.json`, `network.json`, and `controls.json` retain existing compatibility and user options. `Config\Original` holds initial masters; working ShellData and GameData remain live state. Historical `network-observation.json` is unused by this native runtime.

Useful diagnostics: `diagnostics.json`, `shell-lifecycle.json`, `loader-*.log`, `io-*.log`, `unity-*.json` and `unity-*.log`. Native bootstraps exit after initialization. Close the loader to request TEST, wait for operator mode, and close owned windows. Force termination is a logged fallback after orderly shutdown times out.

## Build (development only)

C#/.NET 8 WinForms keeps the small Windows UI and configuration code straightforward. Self-contained publication removes a separate .NET installation requirement. Native x86/x64 C++ modules use the static CRT and exact-build guards.

Run `powershell -ExecutionPolicy Bypass -File .\build.ps1` from a fresh repository checkout. See `docs/BUILD.md` in the repository for prerequisites, outputs and clean-build results. The output contains launcher-owned files only; original game files must be supplied separately.

Close the runtime before replacing launcher binaries. Preserve existing Launcher/Config files when upgrading. `--preflight` validates presence and generates working configuration without launching Shell; it is not a full hash/CRC/runtime test. Build/hash validation also occurs on launch.

Production sources are under `Development/loader-src`. Historical research scripts, captures and proprietary runtime data are intentionally excluded from Git.
