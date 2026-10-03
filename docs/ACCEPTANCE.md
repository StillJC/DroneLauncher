# Acceptance record

## Final handoff build and smoke — 2026-10-02, America/New_York

**Clean Release build: PASS. Bounded smoke: PASS.** Final binary identities are in [COMPONENT_HASHES.json](COMPONENT_HASHES.json). Two independent staged-source snapshots contained no original game payload and no prior bin/obj outputs. Both published the .NET 8 x64 launcher, compiled x86/x64 native components and passed the generated-fixture source regressions. The second assembled runtime-pack notices as well. The owner subsequently selected MIT; the license and corrected notices were included in the payload. A third fresh-source build after the MIT/package update also passed. All five binaries were byte-identical to the smoke-tested artifacts; no additional gameplay retest was needed for the documentation/license-only change.

Verified architecture headers: DroneLauncher x64, IO DLL/bootstrap x86, Unity DLL/bootstrap x64. Product metadata is Drone Launcher 1.1.0; EXE name DroneLauncher.exe. Managed OriginalFilename remains DroneLauncher.dll (the bundled managed assembly). Embedded logo/icon were present and main UI was visible during the smoke test.

The newly built five components replaced only the launcher components in the private Prototype runtime, with previous copies saved locally. Original game/Shell files were not replaced. Existing working controls/display/output preferences were preserved.

### Bounded real-runtime run

Loader 12744; original Shell 18824; first Unity child 5496; operator-relaunched Unity 18008. Evidence IDs are historical labels, not configuration values.

| Required check | Result / evidence |
|---|---|
| Launcher -> Shell, checksum, IO | PASS; native bootstrap, IOReady=1, computed /k, original Shell |
| Original verification | PASS; original log completed verification at 20:40:51 local |
| Title/attract and compatibility | PASS; original GAME_MSG_ATTRACT_START at 20:41:16, eight redirects active, mapped input |
| Start and Quick Race | PASS after a normal test Coin input; original Quick Race (Solo), course/drone flow, race Started at 20:43:48 |
| Steering | PASS; native local values reached +1/-1 and returned to 0 during gameplay |
| Vertical | PASS; Dive -1 / Climb +1 and release to 0 during gameplay |
| Shared Trigger/Boost | PASS; original BOOST_ACTIVATED at 20:44:16.039 and DEACTIVATED at 20:44:16.771 |
| TEST -> original operator | PASS; first game exited normally into state 34 |
| Operator EXIT -> fresh child | PASS; same Shell created Unity 18008; native compatibility active; attract at 20:45:09 |
| Recent optional settings | PASS for initialization; standalone, DISPLAY2 windowed 1280x720/Ultra; output observer initialized; absent loopback TCP receiver remained nonblocking |
| Normal close | PASS; launcher requested TEST, then original window close; Unity 18008 exit 0 at 20:45:31; Shell exit 0 at 20:45:32 |
| No forced termination/orphans | PASS; shutdown log has no timeout termination; owned processes absent afterward |

Native first-child status after race inputs: Active=true, Mapped=true, Failures=0, Fallbacks=0, mode 3, quality 5. This short run intentionally did not complete another full race or inject crashes. Earlier full-race acceptance is summarized below.

The initial automation readiness deadline (150 seconds) expired while original verification/startup was still progressing. The existing session completed successfully; it was observed to title and the bounded test continued without modifying verification or restarting Shell. The first Start attempt had zero credits; a normal Coin input was then used. These harness/session conditions are recorded rather than hidden as clean first-attempt automation.

Raw local evidence is intentionally not published: loader/native logs for those IDs, original Updates/GameStateMachine logs dated 261003 (original logs use UTC), and acceptance directories for Launch/Start/race-input/operator/Close. No personal desktop screenshots or game payload are tracked.

### Original-file integrity

**Final post-smoke audit: PASS, 94/94 original CRC entries and all eleven protected-file comparisons.** No protected source/runtime file mismatches were found. Local report: `artifacts/final-runtime-integrity.json` (ignored; contains private installation paths). That tool recomputes all 94 original manifest entries with the original seeded CRC table and compares eleven protected files with a separate user-supplied source installation. It never edits either runtime.

Reproduce against your own source/runtime copies:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\AuditRuntime.ps1 -Root '<working runtime>' -OriginalRoot '<untouched content root>' -Output '<local report.json>'
```

## Earlier feature acceptance retained as evidence

- Completed Quick Race under Shell 15352 / Unity 17044: start 19:27:49, finish 19:31:26, Game Over 19:32:09, return attract 19:32:16 local. Three TEST/operator/EXIT cycles initialized every new child. This earlier build is not represented as the final handoff EXE.
- Original Input Test: X 0/1604/3703; Y 8/1755/3497; full endpoints and center. Original calibration preserved. Trigger, Secondary, Coin, Service and Test showed OFF/ON/release; Start is decoded but not separately labeled.
- Physical Xbox controls confirmed by user; XInput 0/WinMM 0 duplicate interfaces disappeared together. Separate generic Switch Pro clone on joystick 1 reached normalized -1..1, mapped face-button actions and neutral on release. Latest single-page physical capture and tuning review remains incomplete.
- Single-page editor's actual double-click keyboard capture: Left/Right, neutral with both, neutral on release; cancel preserved previous assignment; closing unsaved left controls.json identical. Alias/custom-action preservation also passes source regression.
- Original Sound Settings music 100 -> 5 -> 100 persisted across operator relaunches. Exit Cancel, Don't ask again, restart/immediate exit and prompt re-enable were exercised; physical Exit prompt and every gameplay context remain incomplete.
- Original Output Test physical rumble ON/OFF confirmed by user. Real output RGB/billboard/vibration levels mapped to TCP. Receiver absence, malformed incoming bytes and reconnect were tested. Rumble reconnect/fatal-process zero remain unverified.
- Secondary-drive runtime root with spaces (137 characters) reached title/native initialization and normal exit; missing monitor fell back to Primary. Three transient input fallbacks were seen there, with no native failures. Arbitrary long/Unicode paths are not claimed.

### Display matrix

| Request | Observed |
|---|---|
| Non-primary Windowed 1280x720, Very Low | mode 3, quality 0 |
| Non-primary Borderless 1920x1080, Low | mode 1, quality 1 |
| Non-primary Exclusive 1280x720, Medium | mode 1 at desktop 2560x1080; quality 2; exclusive failed |
| Primary Exclusive 1920x1080, High | mode 1, quality 3; exclusive failed |
| Primary Windowed 1280x720, Very High | mode 3, quality 4 |
| Non-primary Windowed 1280x720, Ultra | mode 3, quality 5 |

All six actual quality names were tested. Monitor choice survived full restarts and three operator cycles. CLI-only quality was not reliable; original RootScene overrides startup settings, so guarded optional wrappers apply the final selection. Exclusive is not offered. Actual OS DPI transitions were not tested; 100/125/150% renders were only layout simulations.

## Acceptance limits

No two-peer LAN gameplay. No deliberate fatal Unity/Shell test. No new physical hardware claims from the final smoke. No generic force feedback. No universal cabinet-output backend. These remain explicit TODOs in HANDOFF.md and FEATURE_STATUS.md; the closeout does not broaden feature development.
