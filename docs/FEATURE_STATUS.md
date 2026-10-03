# Feature status

Evidence distinguishes implementation from actual tests. Prior feature acceptance used the same native design; final handoff smoke results are recorded separately in [ACCEPTANCE.md](ACCEPTANCE.md). No hardware success is inferred from device enumeration.

| Feature | Implementation Status | Tested | Test Method | Known Limitation |
|---|---|---|---|---|
| Portable startup/root discovery | WORKING / VERIFIED | Yes | Direct/nested fixture layouts; runtime spaces and secondary drive | Original ANSI/scanner restrictions |
| Shell hash validation | WORKING / VERIFIED | Yes | Supported hash and unsupported fixture rejection | One exact Shell build |
| GameAssembly hash validation | WORKING / VERIFIED | Yes | Startup hash and unsupported fixture rejection | One exact GameAssembly build |
| Shell checksum handoff | WORKING / VERIFIED | Yes | Computed /k=186CD76A and native startup | Build-specific checksum |
| Shell CRC verification | WORKING / VERIFIED | Yes | Original startup and independent 94-entry algorithm | Game payload external |
| x86 IO compatibility | WORKING / VERIFIED | Yes | Real Shell handshake and original input/operator pages | Known MkII protocol/build |
| Unity startup/native compatibility | WORKING / VERIFIED | Yes | Real startup and repeated fresh children; guard rejections | Exact signatures only |
| INI/GameData redirection | WORKING / VERIFIED | Yes | Eight observed sites; race/save/portable startup | Narrow original paths only |
| Keyboard controls | WORKING / VERIFIED | Yes | Actual menus, race, original Input Test | Owned-window foreground scope |
| XInput controls | IMPLEMENTED / PARTIALLY VERIFIED | Yes, endpoints/buttons | User moved physical Xbox and pressed controls | Latest UI capture/tuning checks pending |
| Generic joystick | IMPLEMENTED / PARTIALLY VERIFIED | Yes, separate device | User tested Switch Pro clone on WinMM joystick 1 | Enumeration ID may change; no force feedback |
| Steering | WORKING / VERIFIED | Yes | Physical -1..1/center; keyboard/race; two-key preview | Opposite direction inputs cancel |
| Vertical Dive/Climb | WORKING / VERIFIED | Yes | Physical endpoints, original calibration and race | Up=Dive; not selection-page navigation |
| Trigger/Confirm | WORKING / VERIFIED | Yes | Actual selections and physical input | Combined with Boost |
| Start | WORKING / VERIFIED | Yes | Actual title -> selection flow | Not separately labeled in original Input Test |
| Boost | WORKING / VERIFIED | Yes | Original BOOST_ACTIVATED/DEACTIVATED from shared Z trigger | No artificial rumble synthesis |
| Secondary | WORKING / VERIFIED (raw button) | Yes, raw | Original input page/decoder | Not original SelectFlow Back |
| Coin | WORKING / VERIFIED | Yes | Physical button and original rising coin counter | Cabinet accounting retained |
| Service | WORKING / VERIFIED | Yes | Physical input and original menu selection | Chooses item, not activation |
| Test | WORKING / VERIFIED | Yes | Physical input; original operator/relaunch cycles | Original lifecycle retained |
| Control editor | IMPLEMENTED / PARTIALLY VERIFIED | Keyboard/UI yes | Double-click capture, two directions, cancellation, persistence regression | Latest physical capture review pending |
| Live control test | IMPLEMENTED / PARTIALLY VERIFIED | Yes | Recorded Xbox/generic and edited keyboard preview | Active game uses active bindings; raw diagnostics may duplicate interfaces |
| Exit binding | IMPLEMENTED / PARTIALLY VERIFIED | Logical controller yes | Physical RightThumb state; keyboard/button validation | Physical prompt/race-key context incomplete |
| Exit confirmation | WORKING / VERIFIED | Yes | Cancel, Don't ask again, restart, re-enable | Latest checkbox layout shares existing implementation |
| Graceful shutdown | WORKING / VERIFIED (normal) | Yes | Operator/title/race lifecycle closures; no successful-run timeout kills | Fatal-process cleanup not deliberately tested |
| Original operator menu | WORKING / VERIFIED | Yes | TEST -> original UI -> EXIT | Service/Test semantics retained |
| Audio settings | WORKING / VERIFIED | Yes | Original MusicVolume 100 -> 5 -> 100 across relaunch | Launcher guides original Sound Settings; no replacement mixer |
| Resolution selection | WORKING / VERIFIED (tested modes) | Yes | 1280x720 and 1920x1080 observations | Every enumerated mode not tested |
| Monitor selection | WORKING / VERIFIED (tested displays) | Yes | Primary/non-primary, restarts, cycles, missing target fallback | OS display topology beyond tested system unknown |
| Fullscreen exclusive | NOT IMPLEMENTED as supported option | Requested/test failed | Both monitors resolved to Borderless | Hidden from UI; config parser retains legacy acceptance |
| Borderless | WORKING / VERIFIED | Yes | Actual Unity mode 1 | Original game default |
| Windowed | WORKING / VERIFIED | Yes | Actual Unity mode 3 on both displays | Original startup needs guarded wrapper |
| Quality selection | WORKING / VERIFIED | Yes, all six | Actual quality indices 0–5 | CLI alone unreliable; original default Very High |
| Standalone networking | WORKING / VERIFIED | Yes | Title, complete Quick Race, return to attract | Original local TCP retained |
| LAN/link mode | IMPLEMENTED / PARTIALLY VERIFIED | Configuration only | Original-field apply-once/preservation regression; prior UDP bind observation | No two-peer gameplay |
| Native output observer | IMPLEMENTED / PARTIALLY VERIFIED | Actual levels yes | Original Output Test RGB/vibration/billboard and race | Decorative patterns unresolved |
| Controller rumble | IMPLEMENTED / PARTIALLY VERIFIED | Physical ON/OFF yes | User verified original Output Test, XInput slot 0 | Physical reconnect/fatal-exit zero pending |
| TCP output | IMPLEMENTED / PARTIALLY VERIFIED | Yes | Actual loopback events, absent receiver, malformed bytes, reconnect | State stream; bounded queue can drop transitions |
| Local output backend | NOT IMPLEMENTED beyond XInput | Investigation only | Standard XInput/HID API review | HID cabinet reports are hardware-specific |
| Operator relaunch | WORKING / VERIFIED | Yes | Three cycles under same Shell; every child initialized | Longer soak pending |
| Secondary drive | WORKING / VERIFIED (tested path) | Yes | D: startup/native activation/normal exit | Final handoff smoke uses main work copy |
| Long path | WORKING / VERIFIED (tested path) | Yes | 137-character Runtime root with spaces | Not arbitrary extended-length or Unicode support |
| Self-contained deployment | WORKING / VERIFIED | Yes | Clean source-only publish, five-component artifact inspection | Original game's own prerequisites separate |
| DPI behavior | IMPLEMENTED / PARTIALLY VERIFIED | Simulated only | 100/125/150% size/font renders | Actual OS scale transitions not tested |
