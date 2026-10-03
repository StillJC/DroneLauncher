param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts'))
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'Development/loader-src'
$output = [IO.Path]::GetFullPath($OutputDirectory)
$runtime = Join-Path $output 'Runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
Push-Location $PSScriptRoot
try {
    & dotnet --version
    & dotnet publish (Join-Path $source 'DroneRacingGenesisLoader/DroneRacingGenesisLoader.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $runtime
    if ($LASTEXITCODE -ne 0) { throw 'Launcher publish failed.' }
    foreach ($component in @(@{Source='IOCompatibility';Target='IO';Arch='x86';Files=@('DroneRacingGenesis.IO.dll','DroneRacingGenesis.IOBootstrap.exe')},@{Source='NativeUnity';Target='Unity';Arch='x64';Files=@('DroneRacingGenesis.Unity.dll','DroneRacingGenesis.UnityBootstrap.exe')})) {
        $native = Join-Path $output ('native/' + $component.Arch)
        & (Join-Path $source ($component.Source + '/build.cmd')) $native
        if ($LASTEXITCODE -ne 0) { throw ('Native build failed: ' + $component.Source) }
        $destination = Join-Path $runtime ('Launcher/Plugins/' + $component.Target)
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        foreach ($name in $component.Files) { Copy-Item -LiteralPath (Join-Path $native $name) -Destination $destination -Force }
    }
    $config = Join-Path $runtime 'Launcher/Config'
    New-Item -ItemType Directory -Path $config -Force | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'config') -Filter '*.json') {
        $destination = Join-Path $config $file.Name
        if (!(Test-Path -LiteralPath $destination)) { Copy-Item -LiteralPath $file.FullName -Destination $destination }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs/RUNTIME.md') -Destination (Join-Path $runtime 'Launcher/README.md') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $runtime 'Launcher/LICENSE') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $runtime 'Launcher/THIRD_PARTY_NOTICES.md') -Force
    # Carry notices from the actual resolved runtime packs, not an unrelated SDK version.
    $assets = Get-Content -LiteralPath (Join-Path $source 'DroneRacingGenesisLoader/obj/project.assets.json') -Raw | ConvertFrom-Json
    foreach ($framework in $assets.project.frameworks.PSObject.Properties.Value) {
        foreach ($package in $framework.downloadDependencies | Where-Object { $_.name -match '^Microsoft\.(NETCore|WindowsDesktop)\.App\.Runtime\.win-x64$' }) {
            $version = $package.version.Trim('[',']').Split(',')[0].Trim()
            $packagePath = $null
            foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
                $candidate = Join-Path $folder ($package.name.ToLowerInvariant() + '/' + $version)
                if (Test-Path -LiteralPath $candidate) { $packagePath = $candidate; break }
            }
            if (!$packagePath) { throw ('Runtime pack notices missing: ' + $package.name) }
            $notices = @(Get-ChildItem -LiteralPath $packagePath -File | Where-Object { $_.Name -match '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' })
            if (!$notices.Count) { throw ('No license found for ' + $package.name) }
            $destination = Join-Path $runtime ('Launcher/Licenses/' + $package.name + '-' + $version)
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            foreach ($notice in $notices) { Copy-Item -LiteralPath $notice.FullName -Destination $destination -Force }
        }
    }
    & dotnet run --project (Join-Path $source 'DroneRacingGenesisLoader.Tests/DroneRacingGenesisLoader.Tests.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Source regression tests failed.' }
    $files = @('DroneLauncher.exe','Launcher/Plugins/IO/DroneRacingGenesis.IO.dll','Launcher/Plugins/IO/DroneRacingGenesis.IOBootstrap.exe','Launcher/Plugins/Unity/DroneRacingGenesis.Unity.dll','Launcher/Plugins/Unity/DroneRacingGenesis.UnityBootstrap.exe')
    $hashes = foreach ($file in $files) { [pscustomobject]@{File=$file;SHA256=(Get-FileHash -LiteralPath (Join-Path $runtime $file) -Algorithm SHA256).Hash} }
    $hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'component-hashes.json') -Encoding UTF8
    Write-Output "Build and source tests passed. Launcher-only payload: $runtime"
} finally { Pop-Location }
