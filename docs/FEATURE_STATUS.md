# Feature status

Evidence distinguishes implementation from actual tests. Final smoke and handoff results are recorded separately in [ACCEPTANCE.md](ACCEPTANCE.md). No hardware success is inferred from device enumeration alone.

| Feature | Implementation Status | Tested | Test Method | Known Limitation |
|---|---|---|---|---|
| Portable startup/root discovery | WORKING / VERIFIED | Yes | Direct/nested layouts; spaces; secondary drive; real runtime | Original ANSI/scanner restrictions |
| Shell startup handoff | WORKING / VERIFIED | Yes | Real Shell startup with `/k=186CD76A` | Supported Shell build/signatures |
| Shell build compatibility | WORKING / VERIFIED | Yes | Real supported Shell plus guarded native signatures | Build-specific runtime patches |
| GameAssembly compatibility | WORKING / VERIFIED | Yes | Real Unity startup and repeated fresh children | Native build/signature guards remain |
| Shell/game binaries unchanged on disk | WORKING / VERIFIED | Yes | Runtime inspection and implementation review | In-memory hooks/patches are intentionally used |
| x86 IO compatibility | WORKING / VERIFIED | Yes | Real Shell handshake and original input/operator pages | Known MkII protocol/build |
| Unity startup/native compatibility | WORKING / VERIFIED | Yes | Real startup and repeated fresh children | Exact native signatures required |
| INI/GameData redirection | WORKING / VERIFIED | Yes | Observed path sites; race/save/portable startup | Narrow original paths only |
| Startup verification wait bypass | WORKING / VERIFIED | Yes | State 15 original long verification reduced to success cleanup | Supported Shell state/signature only |
| Shell two-second startup delay bypass | WORKING / VERIFIED | Yes | State 3 delay reduced to immediate continuation | Guarded in-memory patch |
| Shell state-20 delay bypass | WORKING / VERIFIED | Yes | Original ~1-second wait reduced to immediate continuation | Guarded in-memory patch |
| Shell pre-launch delay bypass | WORKING / VERIFIED | Yes | State 30 original ~5-second timer removed | Guarded in-memory patch |
| Pre-launch checksum calculation bypass | WORKING / VERIFIED | Yes | Unused state-30 checksum call skipped | Build-specific patch |
| Original network initialization timing | PRESERVED | Yes | Real standalone and linked-cabinet launches | Intentionally not bypassed |
| Keyboard controls | WORKING / VERIFIED | Yes | Actual menus, race and original Input Test | Owned-window foreground scope |
| XInput controls | WORKING / VERIFIED | Yes | Physical Xbox controller; endpoints/buttons/gameplay | Device slot can vary |
| Generic joystick | WORKING / VERIFIED | Yes | Physical WinMM controller testing | Enumeration ID may change; no force feedback |
| Steering | WORKING / VERIFIED | Yes | Physical axis, keyboard, race and preview | Opposite digital directions cancel |
| Vertical Dive/Climb | WORKING / VERIFIED | Yes | Physical endpoints, calibration and race | Up=Dive; Down=Climb |
| Trigger / Confirm | WORKING / VERIFIED | Yes | Actual selections and physical input | Combined with Boost |
| Start | WORKING / VERIFIED | Yes | Actual title-to-selection flow | Not separately labeled in original Input Test |
| Boost | WORKING / VERIFIED | Yes | Original boost activation from shared trigger | Original behavior retained |
| Secondary | WORKING / VERIFIED (raw button) | Yes | Original input page/decoder | Not proven as selection-menu Back |
| Coin | WORKING / VERIFIED | Yes | Physical button and original coin counter | Cabinet accounting retained |
| Service | WORKING / VERIFIED | Yes | Physical input and original menu navigation | Chooses item |
| Test | WORKING / VERIFIED | Yes | Physical input and original operator flow | Activates selected operator item |
| Control editor | WORKING / VERIFIED | Yes | Double-click capture, dual directions, cancel and persistence | Generic device identity remains WinMM-index based |
| Live control test | WORKING / VERIFIED | Yes | Xbox/generic controller and edited keyboard preview | Raw diagnostics can expose duplicate interfaces |
| Exit binding | WORKING / VERIFIED | Yes | Keyboard/controller binding and live cabinet session | One assigned key/button only |
| Exit confirmation | WORKING / VERIFIED | Yes | Cancel, Don't ask again, restart, re-enable | None known in tested flow |
| Immediate owned-process exit | WORKING / VERIFIED | Yes | Configured Exit shuts down active session | Intentional hard termination of owned process trees |
| No-GUI launch | WORKING / VERIFIED | Yes | `DroneLauncher.exe -nogui` real launch | Uses Windows primary display |
| No-GUI Exit binding | WORKING / VERIFIED | Yes | Assigned Exit button ended live no-GUI session | Uses saved control configuration |
| Original operator system | WORKING / VERIFIED | Yes | Service/Test navigation and original operator pages | Launcher no longer provides replacement operator button |
| Audio/operator settings | WORKING / VERIFIED | Yes | Original operator Sound Settings | No replacement launcher audio UI |
| Normal UI monitor placement | WORKING / VERIFIED | Yes | Launcher moved between displays; Shell/game followed launcher | Depends on current Windows display topology |
| No-GUI monitor behavior | WORKING / VERIFIED | Yes | Real no-GUI launch | Uses Windows primary display |
| Standalone networking | WORKING / VERIFIED | Yes | Title, complete Quick Race and normal return flow | Original networking retained |
| LAN configuration | WORKING / VERIFIED | Yes | Real two-PC setup with IDs 1/2 and NumCabinets=2 | Unique cabinet IDs required |
| LAN peer discovery/link | WORKING / VERIFIED | Yes | Two real PCs on same subnet linked successfully | Link may take time to settle |
| LAN attract synchronization | WORKING / VERIFIED | Yes | Both linked machines reached synchronized attract behavior | Not necessarily frame-identical |
| Multiplayer-ready state | WORKING / VERIFIED | Yes | Both linked cabinets displayed multiplayer readiness | Complete linked race not yet tested |
| Two-PC Ethernet/Wi-Fi mixed link | WORKING / VERIFIED | Yes | One cabinet Ethernet, one Wi-Fi, same LAN | Router/network must permit peer communication |
| Native output observer | WORKING / VERIFIED | Yes | Original Output Test RGB/vibration/billboard and race | Some decorative patterns unresolved |
| Controller rumble | WORKING / VERIFIED | Yes | Physical XInput rumble from original output state | Generic joystick force feedback not implemented |
| TCP output server | WORKING / VERIFIED | Yes | Local client connection, current-state snapshot and transitions | Drone Launcher-specific protocol |
| TCP current-state snapshot | WORKING / VERIFIED | Yes | Snapshot sent when client connects | Snapshot occurs once per connection |
| TCP transition-only streaming | WORKING / VERIFIED | Yes | State transitions observed after connection | No recurring periodic refresh |
| Local output HTTP monitor | WORKING / VERIFIED | Yes | Browser/API on localhost port 8765 | Localhost only |
| Output JSON API | WORKING / VERIFIED | Yes | `/api` state endpoint | Localhost only |
| MAME Hooker / OutputHooker protocol compatibility | NOT CLAIMED | N/A | Protocol reviewed as Drone Launcher-specific JSON | External adapter would be required |
| Operator relaunch | WORKING / VERIFIED | Yes | Repeated Shell/game child cycles | Longer soak always possible |
| Secondary drive | WORKING / VERIFIED | Yes | Real launch from non-system drive | Storage speed can affect Unity load time |
| USB/removable execution | FUNCTIONAL / LIMITED | Yes | Real second-machine LAN test from USB media | Slow media may affect Unity asset loading/render timing |
| Long path | WORKING / VERIFIED | Yes | Long Runtime path with spaces | Not arbitrary extended-length or Unicode support |
| Self-contained deployment | WORKING / VERIFIED | Yes | Clean publish and real copied runtime | Original game's own prerequisites remain separate |
| Original AppData behavior | PRESERVED | Yes | Unity continues using normal LocalLow/registry behavior | Runtime is not completely portable |

## Current validation summary

Verified on real runtime hardware:

- normal launcher startup
- `-nogui` startup
- configured Exit handling in normal and no-GUI sessions
- keyboard controls
- XInput controls
- generic WinMM joystick input
- original operator menu
- original game lifecycle
- XInput rumble
- TCP output server
- local HTTP output monitor
- standalone gameplay
- two-machine cabinet linking
- linked attract-mode synchronization
- multiplayer-ready linked state
- monitor placement for normal UI launches
- Windows-primary-display behavior for no-GUI launches
- guarded Shell startup-delay reductions

A complete linked multiplayer race remains to be tested.

## Supported-build policy

The supported target remains the known original Drone Racing Genesis build.

Known Shell SHA256:

```text
59BF7C7676A4AAEC584B4FAF81CDBAF36A6F9B267E69BC0D01250989053AF82B
```

Known GameAssembly SHA256:

```text
491F7C3E3AB392F78AEA8B3B9775B65AAD63ACDE2AE4E2FC4D04FDFE716FB652
```

Whole-file hashes identify the known supported originals, but runtime compatibility also relies on narrow native signature/build checks.

Original executables are never modified on disk.

Guarded in-memory changes are intentionally used where required for compatibility and startup behavior.
