# Launcher release output map

`build.ps1` generates this payload without proprietary files. Paths in the first column are relative to its output directory (default `artifacts`).

| Build output | Destination relative to Runtime | Architecture | Purpose |
|---|---|---|---|
| `Runtime/DroneLauncher.exe` | `DroneLauncher.exe` | x64 / self-contained .NET 8 | UI, configuration, lifecycle, input/output broker |
| `native/x86/DroneRacingGenesis.IO.dll` | `Launcher/Plugins/IO/DroneRacingGenesis.IO.dll` | x86 | Shell IO/output and owned-child adaptation |
| `native/x86/DroneRacingGenesis.IOBootstrap.exe` | `Launcher/Plugins/IO/DroneRacingGenesis.IOBootstrap.exe` | x86 | Suspended Shell initialization |
| `native/x64/DroneRacingGenesis.Unity.dll` | `Launcher/Plugins/Unity/DroneRacingGenesis.Unity.dll` | x64 | Guarded Unity paths/input/optional graphics |
| `native/x64/DroneRacingGenesis.UnityBootstrap.exe` | `Launcher/Plugins/Unity/DroneRacingGenesis.UnityBootstrap.exe` | x64 | Suspended game initialization |
| `Runtime/Launcher/Config/*.json` from `config/*.json` | `Launcher/Config/*.json` | data | First-install defaults; do not overwrite existing user settings |
| `Runtime/Launcher/README.md` from `docs/RUNTIME.md` | `Launcher/README.md` | text | Runtime usage |
| `Runtime/Launcher/LICENSE` from root `LICENSE` | `Launcher/LICENSE` | text | MIT license for project and owner-created branding |
| `Runtime/Launcher/THIRD_PARTY_NOTICES.md` | `Launcher/THIRD_PARTY_NOTICES.md` | text | Distribution audit |
| `Runtime/Launcher/Licenses/*` | `Launcher/Licenses/*` | text | Notices from actual resolved runtime packs |

The wrapper copies native EXE/DLL products into the matching Runtime destinations; `.obj`, `.lib`, `.exp` and guard_test are development outputs, not payload. The managed DroneRacingGenesis.Plugin is bundled by single-file publish; no external managed plugin DLL is required. Logs and initial original-config backups are generated at runtime.

Shell.exe, DK2WIN32.dll, DroneRacing.exe, UnityPlayer.dll, GameAssembly.dll, install manifests, game assets, ShellData, GameData and backup metadata are **user-supplied**, never outputs of this build. Build hashes are in COMPONENT_HASHES.json; original compatibility hashes are in HANDOFF.md. The owner confirmed authorship of the branding and selected MIT; preserve project and runtime-pack notices in any formal binary release.
