# Build instructions

Drone Launcher builds entirely from the public source repository.

Original Drone Racing Genesis game files are **not** required to build the launcher.

## Prerequisites

Required development environment:

- Windows x64
- PowerShell 5.1 or later
- Git
- .NET SDK **8.0.420**
- Visual Studio with **Desktop development with C++**
- x86 and x64 MSVC toolchains
- Windows SDK

The repository root `global.json` selects the .NET SDK feature band. Later compatible 8.0.4xx patches may also work.

Tested development environment:

```text
Visual Studio Community 2026 18.10.2
MSVC 14.51.36231
Windows SDK 10.0.26100.0
.NET SDK 8.0.420
```

The native build scripts use `vswhere` to locate Visual Studio and call the appropriate x86/x64 Visual C++ environment.

The build does not require:

- CMake
- Ninja
- Python
- Frida
- proprietary game files
- an existing Drone Racing Genesis installation
- pre-existing output directories

## Fresh clone build

```powershell
git clone https://github.com/StillJC/DroneLauncher.git
cd DroneLauncher
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The build script performs the complete release build.

It:

- restores required .NET packages/runtime packs
- publishes the self-contained x64 launcher
- builds the x86 IO native components
- builds the x64 Unity native components
- assembles `artifacts\Runtime`
- copies release configuration defaults
- copies required runtime/license notices
- runs the source regression tests
- writes component hashes

The build script does **not** install files into any external game directory.

## Output

Primary release output:

```text
artifacts\Runtime\
```

Expected launcher components include:

```text
artifacts\Runtime\DroneLauncher.exe

artifacts\Runtime\Launcher\Plugins\IO\
  DroneRacingGenesis.IO.dll
  DroneRacingGenesis.IOBootstrap.exe

artifacts\Runtime\Launcher\Plugins\Unity\
  DroneRacingGenesis.Unity.dll
  DroneRacingGenesis.UnityBootstrap.exe
```

Architecture:

```text
DroneLauncher.exe                         x64
DroneRacingGenesis.IO.dll                x86
DroneRacingGenesis.IOBootstrap.exe       x86
DroneRacingGenesis.Unity.dll             x64
DroneRacingGenesis.UnityBootstrap.exe    x64
```

The launcher is published as a self-contained .NET 8 Windows application.

Users do not need to install .NET separately.

The native helpers use the static Visual C++ runtime.

## Custom output directory

A different output location can be supplied with:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -OutputDirectory 'C:\Temp\DroneLauncherBuild'
```

Use a new/empty output directory when performing a clean verification build.

## Underlying managed build

The main launcher publish is equivalent to:

```powershell
dotnet publish Development/loader-src/DroneRacingGenesisLoader/DroneRacingGenesisLoader.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -o artifacts/Runtime
```

The main project targets:

```text
net8.0-windows
```

The repository-owned supporting managed plugin targets:

```text
netstandard2.1
```

## Native IO build

The x86 Shell/IO compatibility components are built with:

```powershell
& .\Development\loader-src\IOCompatibility\build.cmd "$PWD\artifacts\native\x86"
```

These produce:

```text
DroneRacingGenesis.IO.dll
DroneRacingGenesis.IOBootstrap.exe
```

Architecture:

```text
x86
```

## Native Unity build

The x64 Unity compatibility components are built with:

```powershell
& .\Development\loader-src\NativeUnity\build.cmd "$PWD\artifacts\native\x64"
```

These produce:

```text
DroneRacingGenesis.Unity.dll
DroneRacingGenesis.UnityBootstrap.exe
```

Architecture:

```text
x64
```

## Native compiler settings

The native components use flags equivalent to:

```text
/W4
/O2
/MT
/EHsc
/std:c++17
```

DLL targets additionally use:

```text
/LD
```

The linker uses reproducible-build support where available:

```text
/Brepro
```

Build intermediates and products remain ignored by Git.

## Regression tests

The source regression executable can be run independently with:

```powershell
dotnet run --project Development/loader-src/DroneRacingGenesisLoader.Tests/DroneRacingGenesisLoader.Tests.csproj -c Release
```

The tests use generated temporary fixtures.

No proprietary game payload is required.

Current regression coverage includes areas such as:

- root/layout handling
- paths containing spaces
- preflight behavior
- configuration preservation
- input mapping
- control persistence
- network configuration
- output-ring handling
- session reset behavior
- stale-process/error isolation
- diagnostics-sharing recovery

A passing source regression run verifies the launcher implementation against the generated fixtures.

It is not a substitute for real runtime gameplay testing.

Real runtime acceptance is documented in:

```text
docs/ACCEPTANCE.md
```

## Build verification

A clean build should complete all of the following successfully:

```text
Managed self-contained publish
x86 native IO build
x64 native Unity build
Runtime payload assembly
Runtime/license notice assembly
Source regression tests
Component hash generation
```

A build passing these checks means the source tree can produce the expected launcher payload with the documented development prerequisites.

It does not imply byte-identical binaries across different:

- .NET SDK versions
- MSVC versions
- Windows SDK versions
- runtime pack versions

Use the same toolchain versions when comparing component hashes.

## Component hashes

The build generates:

```text
artifacts\component-hashes.json
```

Release documentation may also include:

```text
docs\COMPONENT_HASHES.json
```

These hashes identify a specific built set of launcher binaries.

They should be regenerated after any source change that affects the compiled output.

Do not update the documented release hashes until the final 1.0 build has completed successfully.

## Runtime package assembly

The build payload contains launcher-owned files only.

The user supplies the original game installation separately.

Typical merged runtime:

```text
Runtime/
  DroneLauncher.exe

  Launcher/
    Config/
    Plugins/
      IO/
      Unity/
    Licenses/
    README.md
    THIRD_PARTY_NOTICES.md

  Shell/
  ShellData/
  GameData/
  DroneRacing/
  backup/
```

The following are user-supplied original game content:

```text
Shell/
ShellData/
GameData/
DroneRacing/
backup/
```

These are not part of the repository or launcher build.

## Configuration defaults

Release defaults are sourced from:

```text
config/
```

The build copies defaults into the runtime payload.

Existing live cabinet configuration should be preserved when deploying an update.

Do not blindly overwrite an existing:

```text
Launcher\Config\
```

directory on a configured cabinet.

## Development source locations

Primary managed launcher:

```text
Development\loader-src\DroneRacingGenesisLoader\
```

Managed supporting plugin:

```text
Development\loader-src\DroneRacingGenesis.Plugin\
```

x86 Shell compatibility:

```text
Development\loader-src\IOCompatibility\
```

x64 Unity compatibility:

```text
Development\loader-src\NativeUnity\
```

Regression tests:

```text
Development\loader-src\DroneRacingGenesisLoader.Tests\
```

## Runtime requirements

The launcher itself is self-contained.

No runtime installation of the following is required:

```text
.NET SDK
.NET runtime
Visual Studio
Python
Frida
CMake
Ninja
```

The original game may still rely on its own Windows/game prerequisites.

Those are outside Drone Launcher's build system.

## Warning policy

Compiler warnings should be reviewed, but a known harmless warning is not equivalent to a failed build.

Do not suppress warnings merely to make the build log visually clean.

Any new warning introduced by a source change should be reviewed before release.

## Clean release procedure

Before creating the final 1.0 commit/release:

1. Confirm the working tree contains only intended source/documentation changes.
2. Run a clean build.
3. Confirm all regression tests pass.
4. Confirm x86/x64 native binaries were rebuilt.
5. Confirm the runtime payload contains no proprietary game files.
6. Regenerate component hashes.
7. Update `docs\COMPONENT_HASHES.json` from the final successful build.
8. Perform the final runtime smoke test.
9. Commit the source/documentation/hash update together.

Do not use older component hashes after rebuilding changed source.

## Release references

Related documentation:

```text
README.md
HANDOFF.md
docs/RUNTIME.md
docs/FEATURE_STATUS.md
docs/ACCEPTANCE.md
docs/RELEASE_OUTPUTS.md
docs/COMPONENT_HASHES.json
THIRD_PARTY_NOTICES.md
LICENSE
```

The repository should remain sufficient for a new developer to clone, build, inspect and continue the project without access to private historical development files.