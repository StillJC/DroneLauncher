# Drone Launcher

A Windows launcher and native compatibility layer for the supported **Drone Racing Genesis** installation. It preserves original Shell ownership, game verification, operator menus and game lifecycle while relocating runtime paths and providing configurable keyboard/controller input.

**Original game files are not included.** Supply your own legally obtained complete installation. This repository builds without those files; running the game requires them. It is an independent project, not an official Sega distribution.

## Build

On Windows x64, install .NET SDK **8.0.420** (or a later 8.0.4xx patch allowed by `global.json`) and Visual Studio C++ x86/x64 tools plus a Windows SDK. The tested native toolchain is documented in [Build instructions](docs/BUILD.md).

```powershell
git clone https://github.com/StillJC/DroneLauncher.git
cd DroneLauncher
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output: `artifacts/Runtime/`, containing the self-contained .NET 8 x64 `DroneLauncher.exe`, x86/x64 native helpers, sanitized configuration and notices. Users do not need .NET installed. Build prerequisites are development-only. See [release output map](docs/RELEASE_OUTPUTS.md).

## Runtime setup

Merge the launcher output with a **copy** of your matching original installation:

```text
Runtime/
  DroneLauncher.exe             # built here
  Launcher/                    # built/configured here
  Shell/                       # user-supplied game files
  ShellData/                   # user-supplied, mutable operator state
  GameData/                    # user-supplied, mutable scores/state
  DroneRacing/                 # user-supplied Unity game
  backup/                      # user-supplied original metadata
```

Preserve existing `Launcher/Config` when upgrading. Do not add arbitrary folders at Runtime root: original Shell scans them for games. Keep development/build material elsewhere. Direct and nested `Sega` content layouts are detected from the EXE location.

Start **DroneLauncher.exe**, then **Launch Game**. Double-click a function in Configure Controls to capture a key, button or axis. Default arrows steer/Dive/Climb, Enter starts, Z confirms/boosts, X is Secondary, 5 adds Coin, F1 is Service, F2 is Test and Escape requests Exit. Xbox mappings are also supplied. In operator menus, Service chooses and Test selects. Audio remains in the original operator Sound Settings.

## Supported build and status

- Unity 2019.4.8f1 x64 IL2CPP.
- Shell SHA256: `59BF7C7676A4AAEC584B4FAF81CDBAF36A6F9B267E69BC0D01250989053AF82B`.
- GameAssembly SHA256: `491F7C3E3AB392F78AEA8B3B9775B65AAD63ACDE2AE4E2FC4D04FDFE716FB652`.
- Original game binaries stay unchanged on disk. Unsupported builds are rejected.
- Standalone race/operator flow, keyboard, Xbox and a generic joystick were exercised. Display offers tested Windowed/Borderless and six project quality presets. Rumble/TCP are optional and disabled in shipped defaults.
- Two-machine LAN gameplay, fatal-process cleanup testing, physical capture review of the latest controls UI and real OS DPI transitions remain incomplete. Exclusive fullscreen fell back to Borderless and is not offered. Original ANSI/path-scanner restrictions remain; arbitrary Unicode/extended-length paths are not a supported claim.

Start development with [HANDOFF.md](HANDOFF.md). See [feature status](docs/FEATURE_STATUS.md), [runtime usage](docs/RUNTIME.md), [build verification](docs/BUILD.md), [audit](docs/REPOSITORY_AUDIT.md) and [acceptance results](docs/ACCEPTANCE.md).

## Licensing

Project source and owner-created launcher branding use the [MIT License](LICENSE). See [third-party and distribution audit](THIRD_PARTY_NOTICES.md). No proprietary game payload is part of this repository.
