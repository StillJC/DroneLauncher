# Third-party and distribution audit

## Repository license

The project uses the **MIT License**, at repository root `LICENSE`, selected with the owner's explicit authorization. Copyright holder: StillJC. The owner also confirmed that they created the supplied logo and icon; those project resources are included under the project license. Third-party runtime notices remain separate.

## Included source and artwork

- Production C#/C++ code and source regression tests are the project implementation. No vendored detour library, MinHook, Frida, Python runtime, or third-party NuGet PackageReference is included.
- The .NET SDK generates build intermediates and the app host; those are not tracked. The plugin project is this repository's own managed library, bundled into the launcher publication.
- `Development/loader-src/DroneRacingGenesisLoader/Assets/DroneLauncher.png` and `.ico` are byte-identical copies of the user-supplied Drone Launcher branding. They are not extracted game artwork. The owner explicitly confirmed authorship during handoff and authorized this public source publication.
- No original Sega/Drone Racing Genesis EXEs, DLLs, assets, manuals, screenshots, install manifests, ShellData/GameData or binary dumps are tracked. Small build hashes and instruction signatures identify the supported build; they are compatibility metadata, not an installable game payload.

## Build and release dependencies

.NET runtime and Windows Forms use MIT licensing with upstream notices: [.NET runtime license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT), [runtime third-party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT), [Windows Forms license](https://github.com/dotnet/winforms/blob/main/LICENSE.TXT). The self-contained EXE includes Microsoft runtime components. `build.ps1` copies LICENSE and available THIRD-PARTY-NOTICES from the **resolved runtime packs** into `Runtime/Launcher/Licenses/`; keep that directory with a binary release. The source tree does not redistribute those runtime binaries.

Native modules compile with MSVC `/MT`: the CRT is statically linked; normal Windows system DLL imports remain. MSVC and Windows SDK use Microsoft's toolchain terms; this audit does not replace those terms. Review the installed toolchain's redist terms before distributing compiled native components. No SDK libraries, compiler binaries or caches are checked into Git.

## Distribution scope

No known unresolved licensing/provenance concern was identified for the reviewed source handoff. Preserve the root MIT license and third-party notices in future binary packages. Microsoft toolchain terms still govern its runtime components; no compiler or SDK library files are distributed here.

The current action publishes source and documentation only, not a binary release or original game content.
