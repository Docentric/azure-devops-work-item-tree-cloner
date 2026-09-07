#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
solution="$root/AdoWorkItemTreeCloner.slnx"
test_project="$root/tests/AdoWorkItemTreeCloner.Core.Tests/AdoWorkItemTreeCloner.Core.Tests.csproj"
test_results="$root/artifacts/TestResults"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1

rm -rf "$test_results"
mkdir -p "$test_results"

dotnet restore "$solution"
dotnet format "$solution" --verify-no-changes --no-restore
dotnet build "$solution" --configuration "$configuration" --no-restore
dotnet test "$test_project" \
  --configuration "$configuration" \
  --no-build \
  --results-directory "$test_results" \
  --report-trx \
  --report-trx-filename "{asm}_{tfm}_{arch}.trx" \
  --coverage \
  --coverage-output-format cobertura
