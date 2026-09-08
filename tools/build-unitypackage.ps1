<#
.SYNOPSIS
Packs unity/VRCWakeMe into a .unitypackage for release.

.DESCRIPTION
A .unitypackage is a gzipped tar where every asset lives in a folder named after
its GUID and holds three files: the asset itself, its .meta, and the project
path it should be imported to.
#>
[CmdletBinding()]
param(
    [string]$SourceFolder,
    [string]$AssetRoot = 'Assets/VRCWakeMe',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
if (-not $SourceFolder) { $SourceFolder = Join-Path $repoRoot 'unity\VRCWakeMe' }
if (-not $OutputPath) { $OutputPath = Join-Path $repoRoot 'dist\VRCWakeMe-Avatar.unitypackage' }

$SourceFolder = (Resolve-Path $SourceFolder).Path
$staging = Join-Path ([IO.Path]::GetTempPath()) ("vrcwakeme-unitypackage-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging -Force | Out-Null

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

$outputDir = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
if (Test-Path $OutputPath) { Remove-Item $OutputPath }

tar -czf $OutputPath -C $staging .
if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" }

Remove-Item $staging -Recurse -Force
Write-Host "Wrote $((Resolve-Path $OutputPath).Path)"
