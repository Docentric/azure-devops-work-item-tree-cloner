[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $SkipFormat,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'AdoWorkItemTreeCloner.slnx'
$testProject = Join-Path $root 'tests/AdoWorkItemTreeCloner.Core.Tests/AdoWorkItemTreeCloner.Core.Tests.csproj'
$testResults = Join-Path $root 'artifacts/TestResults'

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'

Push-Location $root
try {
    if (Test-Path $testResults) {
        Remove-Item $testResults -Recurse -Force
    }

    New-Item $testResults -ItemType Directory -Force | Out-Null

    dotnet restore $solution

    if (-not $SkipFormat) {
        dotnet format $solution --verify-no-changes --no-restore
    }

    dotnet build $solution --configuration $Configuration --no-restore

    if (-not $SkipTests) {
        dotnet test $testProject `
            --configuration $Configuration `
            --no-build `
            --results-directory $testResults `
            --report-trx `
            --report-trx-filename '{asm}_{tfm}_{arch}.trx' `
            --coverage `
            --coverage-output-format cobertura
    }
}
finally {
    Pop-Location
}
