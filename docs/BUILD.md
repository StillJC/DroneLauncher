# Build instructions and verification

## Prerequisites

Windows x64, PowerShell 5.1 or later, Git; .NET SDK **8.0.420** (selected by root global.json, later patches in the same feature band allowed). Native scripts locate Visual Studio with `vswhere` and require **Desktop development with C++**, both x86/x64 MSVC tools, and a Windows SDK.

Tested environment: Visual Studio Community 2026 **18.10.2**, MSVC **14.51.36231**, Windows SDK **10.0.26100.0**. `build.cmd` calls vcvars32/vcvars64. No CMake, Ninja, Python, Frida, proprietary game files, or pre-existing output directories are needed. Earlier Visual Studio versions have not been re-verified for this handoff.

## Fresh clone

```powershell
git clone https://github.com/StillJC/DroneLauncher.git
cd DroneLauncher
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The script restores automatically, publishes, compiles both native architectures, assembles `artifacts/Runtime`, copies config defaults only when absent, copies resolved runtime-pack license notices, runs the source tests and writes `artifacts/component-hashes.json`. It never installs into the external game runtime. `-OutputDirectory <directory>` selects another build-output directory; use a new directory for a clean build.

## Underlying commands

```powershell
dotnet publish Development/loader-src/DroneRacingGenesisLoader/DroneRacingGenesisLoader.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o artifacts/Runtime
& .\Development\loader-src\IOCompatibility\build.cmd "$PWD\artifacts\native\x86"
& .\Development\loader-src\NativeUnity\build.cmd "$PWD\artifacts\native\x64"
dotnet run --project Development/loader-src/DroneRacingGenesisLoader.Tests/DroneRacingGenesisLoader.Tests.csproj -c Release
```

Use the wrapper for payload assembly/notices/defaults. Native flags: `/W4 /O2 /MT /EHsc /std:c++17`, DLLs additionally `/LD`, linker `/Brepro`. Build products/intermediates remain ignored. C# project references only the repository-owned netstandard2.1 plugin; framework/runtime packs are SDK restore dependencies. The main project targets net8.0-windows; bootstraps use x86 and x64 as shown in the release map.

A pre-existing C4100 unreferenced-parameter warning in `io_module.cpp` is recorded; it is not a compile error. No warning suppression or compatibility changes were made to hide it.

## Verification record

Initial source-only staged snapshot build: **PASS**, including self-contained publish, both native builds and source regressions. The snapshot had no original game files or prior bin/obj directories. Final independent source-only repeat build: **PASS**, including both native architectures, source regressions and runtime notices. The final closeout fresh-source build after adding MIT packaging also passed; all five component hashes matched the smoke-tested build exactly. SDK 8.0.420 resolved .NET/WindowsDesktop runtime packs 8.0.26. Bounded runtime smoke results are recorded at closeout in [ACCEPTANCE.md](ACCEPTANCE.md); [COMPONENT_HASHES.json](COMPONENT_HASHES.json) identifies final artifacts.

Build success means a fresh source tree with the documented normal prerequisites builds. It is not a promise of byte-identical output across SDK/MSVC versions. Preserve the SDK/toolchain/runtime-pack versions when comparing binary hashes. No binary release is uploaded as part of this source handoff.
