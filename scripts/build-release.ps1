<#
.SYNOPSIS
Builds a release: the app as a single exe, the avatar .unitypackage, and one zip holding both.

.DESCRIPTION
The app is published framework-dependent, so the download stays around a megabyte
and needs the .NET 8 Desktop Runtime on the machine that runs it.

A .unitypackage is a gzipped tar where every asset lives in a folder named after
its GUID and holds three files: the asset itself, its .meta, and the project path
it should be imported to.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$AssetRoot = 'Assets/VRCWakeMe',
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

function New-UnityPackage {
    param(
        [Parameter(Mandatory)][string]$SourceFolder,
        [Parameter(Mandatory)][string]$AssetRoot,
        [Parameter(Mandatory)][string]$OutputPath
    )

    $SourceFolder = (Resolve-Path $SourceFolder).Path
    $staging = Join-Path ([IO.Path]::GetTempPath()) ("vrcwakeme-unitypackage-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    try {
        $assets = Get-ChildItem -Path $SourceFolder -Recurse -File | Where-Object { $_.Extension -ne '.meta' }
        if ($assets.Count -eq 0) { throw "No assets found in $SourceFolder" }

        foreach ($asset in $assets) {
            $metaPath = "$($asset.FullName).meta"
            if (-not (Test-Path $metaPath)) { throw "Missing meta file for $($asset.FullName)" }

            $guid = (Select-String -Path $metaPath -Pattern '^guid:\s*([0-9a-f]{32})' | Select-Object -First 1).Matches[0].Groups[1].Value
            if (-not $guid) { throw "No guid in $metaPath" }

            $relative = $asset.FullName.Substring($SourceFolder.Length).TrimStart('\', '/').Replace('\', '/')
            $entry = Join-Path $staging $guid
            New-Item -ItemType Directory -Path $entry -Force | Out-Null
            Copy-Item $asset.FullName (Join-Path $entry 'asset')
            Copy-Item $metaPath (Join-Path $entry 'asset.meta')
            [IO.File]::WriteAllText((Join-Path $entry 'pathname'), "$AssetRoot/$relative", (New-Object Text.UTF8Encoding $false))

            Write-Host "  $guid  $AssetRoot/$relative"
        }

        New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
        if (Test-Path $OutputPath) { Remove-Item $OutputPath }

        tar -czf $OutputPath -C $staging .
        if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" }
    }
    finally {
        Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$project = Join-Path $repoRoot 'src\VRCWakeMe.csproj'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'dist' }

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version
if (-not $version) { throw "No <Version> in $project" }

$unityPackage = Join-Path $OutputDirectory 'VRCWakeMe-Avatar.unitypackage'
$zipPath = Join-Path $OutputDirectory "VRCWakeMe-$version-$Runtime.zip"
$staging = Join-Path ([IO.Path]::GetTempPath()) ("vrcwakeme-release-" + [Guid]::NewGuid().ToString('N'))

try {
    Write-Host "Publishing the app ($Configuration, $Runtime, framework-dependent)..."
    dotnet publish $project `
        -c $Configuration `
        -r $Runtime `
        --self-contained false `
        -p:PublishSingleFile=true `
        -p:DebugType=none `
        -o $staging | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    Write-Host "`nPacking the avatar assets..."
    New-UnityPackage `
        -SourceFolder (Join-Path $repoRoot 'unity\VRCWakeMe') `
        -AssetRoot $AssetRoot `
        -OutputPath $unityPackage
    Copy-Item $unityPackage $staging

    Write-Host "`nZipping..."
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -Force
}
finally {
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "`nWrote $((Resolve-Path $unityPackage).Path)"
Write-Host "Wrote $((Resolve-Path $zipPath).Path) ($('{0:N2} MB' -f ((Get-Item $zipPath).Length / 1MB)))"
