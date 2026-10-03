# Repository and path audit

## Git target

The supplied Prototype root had no Git metadata. `git ls-remote` and GitHub repository metadata established that **StillJC/DroneLauncher was an empty public repository** with no existing branch history. Local `main` was initialized in place and `origin` set to `https://github.com/StillJC/DroneLauncher`. No old commits, branches or source edits were reset/discarded. Final commit/remote equality is verified after pushing.

## Public content boundary

Root .gitignore uses an allowlist. Tracked content is the current C#/C++ projects/tests, supplied launcher PNG/ICO, sanitized JSON defaults, build scripts and curated documentation/audit helper. Only the two supplied launcher branding files are binary source assets. Native instruction signatures and compatibility hashes are intentionally retained build-identification data.

Never staged: original Shell/Unity/game/DK2 files, original install manifests, mutable ShellData/GameData, game assets/media/manuals, Runtime/backup, frozen full-payload baselines, screenshots, logs, dumps, analysis captures, virtual environments, Frida-era scripts/manifests, SDK caches or build products. Local runtime/research files remain on disk. Obsolete plugin scripts initially found during staged review were removed **from the index only** before any commit.

Pattern scan found no token/private-key candidates in reviewed text. This is a bounded source audit, not a claim of a full security assessment. File contents, staged diff and allowed binary types were reviewed before commit.

## Absolute path review

Production source and committed defaults contain no development drive, user profile, Prototype-root, acceptance-root or stale PID references. Runtime paths come from executable-derived roots and explicit inherited compatibility variables. Development tools take runtime paths as parameters.

Intentional path-like strings:

- Original `C:/Sega/ShellData/...` and `C:/Sega/GameData//` strings in native compatibility/PortablePathMap and test fixtures: exact original paths being **matched and redirected**, never a required installation destination.
- Temporary fixture paths are derived from the test assembly location, bounded before cleanup. Diagnostic temporary files derive from launcher-owned roots.
- `Development/loader-src` is a repository-relative source path in project references/build scripts/docs; no runtime dependency on Development.
- `%ProgramFiles(x86)%` plus vswhere locates the installed Visual Studio toolchain at build time. NuGet pack roots come from generated restore metadata when copying license notices.
- `Local\\DRG.*` identifies per-session local mappings, not a disk path.
- Loopback output host with TCP disabled is a neutral configurable placeholder, not a personal receiver address. Monitor default is empty and input defaults use any XInput slot rather than a test controller index.

No machine-specific S:/D:/profile paths are normal setup instructions in public docs. Historical records with those paths remain local and ignored.

## Intentionally ignored local state

- `Runtime/`: complete private working game copy and mutable configuration/logs.
- `Development/Baselines/`, `Development/Analysis/`, `Development/Build/`, `Development/ResearchLogs/`: rollback payloads, private research, earlier binaries/evidence.
- Original supplied artwork copies directly under Development; the identical curated Assets copies are tracked.
- Root `*_Findings.md` and `DroneRacingGenesis_Runtime_Investigation.md`: historical machine-specific reports, superseded for public handoff by HANDOFF/docs.
- Unselected scripts, `.venv-trace`, `__pycache__`, old plugin agents/manifests and UiPreview under Development/loader-src.
- `bin/`, `obj/`, native `.obj/.lib/.exp` and `artifacts/`: generated intermediates, clean-source verification snapshots and final build products.

These ignored items are not unexplained source changes and do not appear in normal git status. No local game content was deleted to make Git clean.

Owner authorization of MIT and artwork authorship, plus third-party obligations, are recorded in [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md).
