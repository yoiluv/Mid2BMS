[CmdletBinding()]
param(
    [switch]$Accept
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot "build-legacy.ps1"
$runnerPath = Join-Path $repositoryRoot "tests\Mid2BMS.CharacterizationTests\bin\Debug\Mid2BMS.CharacterizationTests.exe"

& $buildScript -Configuration Debug
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$runnerArguments = @("--repository-root", $repositoryRoot)
if ($Accept) {
    $runnerArguments += "--accept"
}

& $runnerPath @runnerArguments
exit $LASTEXITCODE
