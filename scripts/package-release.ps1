[CmdletBinding()]
param(
    [ValidatePattern('^v\d{8}$')]
    [string]$Version = "v20260923",

    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectPath = Join-Path $repositoryRoot "Mid2BMS\Mid2BMS.csproj"
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts\release"))
$packageName = "Mid2BMS-$Version-win-x64"
$packageDirectory = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $packageName))
$zipPath = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot ($packageName + ".zip")))
$checksumPath = $zipPath + ".sha256"
$releaseNotesPath = Join-Path $repositoryRoot ("docs\releases\" + $Version + ".md")

function Assert-ReleasePath([string]$Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    $prefix = $releaseRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Release output escaped the artifacts directory: $resolved"
    }
}

Assert-ReleasePath $packageDirectory
Assert-ReleasePath $zipPath
Assert-ReleasePath $checksumPath

if (-not (Test-Path -LiteralPath $releaseNotesPath)) {
    throw "Release notes are missing: $releaseNotesPath"
}

if (-not $SkipTests) {
    & (Join-Path $PSScriptRoot "test-regression.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "Regression tests failed."
    }
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
foreach ($target in @($packageDirectory, $zipPath, $checksumPath)) {
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}

$publishArguments = @(
    "publish"
    $projectPath
    "--nologo"
    "--configuration", "Release"
    "--runtime", "win-x64"
    "--self-contained", "true"
    "--output", $packageDirectory
    "-p:Platform=x86"
    "-p:PublishTrimmed=false"
    "-p:PublishSingleFile=false"
    "-p:PublishReadyToRun=false"
    "-p:DebugSymbols=false"
    "-p:DebugType=None"
)

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Release publish failed."
}

Get-ChildItem -LiteralPath $packageDirectory -Filter "*.pdb" -File -Recurse |
    Remove-Item -Force

Copy-Item -LiteralPath (Join-Path $repositoryRoot "README.md") -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot "LICENSE") -Destination (Join-Path $packageDirectory "LICENSE.txt")
Copy-Item -LiteralPath $releaseNotesPath -Destination (Join-Path $packageDirectory "RELEASE_NOTES.md")

$licensesDirectory = Join-Path $packageDirectory "licenses"
New-Item -ItemType Directory -Path $licensesDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot "NVorbis\COPYING.txt") `
    -Destination (Join-Path $licensesDirectory "NVorbis-Ms-PL.txt")

$datePart = $Version.Substring(1)
$expectedFileVersion = "{0}.{1}.{2}.0" -f `
    [Int32]$datePart.Substring(0, 4), `
    [Int32]$datePart.Substring(4, 2), `
    [Int32]$datePart.Substring(6, 2)
$executablePath = Join-Path $packageDirectory "Mid2BMS.exe"
$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executablePath)
if ($versionInfo.FileVersion -ne $expectedFileVersion) {
    throw "Unexpected file version. Expected $expectedFileVersion, actual $($versionInfo.FileVersion)."
}
if ($versionInfo.ProductVersion -ne $Version) {
    throw "Unexpected product version. Expected $Version, actual $($versionInfo.ProductVersion)."
}

$buildInformation = @(
    "Product: Mid2BMS"
    "Version: $Version"
    "FileVersion: $expectedFileVersion"
    "Runtime: win-x64"
    "Deployment: self-contained"
)
$buildInformation | Set-Content -LiteralPath (Join-Path $packageDirectory "BUILD_INFO.txt") -Encoding UTF8

Compress-Archive -LiteralPath $packageDirectory -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
("{0}  {1}" -f $hash, [System.IO.Path]::GetFileName($zipPath)) |
    Set-Content -LiteralPath $checksumPath -Encoding ASCII

Write-Host "Release package: $zipPath"
Write-Host "SHA-256: $hash"
