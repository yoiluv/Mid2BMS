[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("x86")]
    [string]$Platform = "x86",

    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "Mid2BMS.sln"
$vswherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$msbuildPath = $null

if (Test-Path -LiteralPath $vswherePath) {
    $msbuildPath = & $vswherePath `
        -latest `
        -products * `
        -requires Microsoft.Component.MSBuild `
        -find "MSBuild\**\Bin\MSBuild.exe" |
        Select-Object -First 1
}

if (-not $msbuildPath) {
    $msbuildCommand = Get-Command "MSBuild.exe" -ErrorAction SilentlyContinue
    if ($msbuildCommand) {
        $msbuildPath = $msbuildCommand.Source
    }
}

if (-not $msbuildPath) {
    throw "MSBuild.exe was not found. Install Visual Studio 2022 or later (or Build Tools) with MSBuild."
}

$msbuildArguments = @(
    $solutionPath
    "-nologo"
    "-m"
    "-v:minimal"
    "-p:Configuration=$Configuration"
    "-p:Platform=$Platform"
)

if (-not $NoRestore) {
    $msbuildArguments += "-restore"
}

Write-Host "MSBuild: $msbuildPath"
Write-Host "Configuration: $Configuration|$Platform"

& $msbuildPath @msbuildArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$outputPath = Join-Path $repositoryRoot "Mid2BMS\bin\$Configuration\Mid2BMS.exe"
Write-Host "Build succeeded: $outputPath"
