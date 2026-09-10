# Contributing

## Before opening a pull request

Run the full engineering build:

```powershell
./eng/build.ps1
```

or:

```bash
./eng/build.sh
```

Manual equivalent:

```bash
dotnet restore AdoWorkItemTreeCloner.slnx
dotnet format AdoWorkItemTreeCloner.slnx --verify-no-changes --no-restore
dotnet build AdoWorkItemTreeCloner.slnx -c Release --no-restore
dotnet test tests/AdoWorkItemTreeCloner.Core.Tests/AdoWorkItemTreeCloner.Core.Tests.csproj \
  -c Release \
  --no-build \
  --report-trx \
  --coverage \
  --coverage-output-format cobertura
```

## Code changes

- Keep runtime behavior in `AdoWorkItemTreeCloner.Core`.
- Keep terminal/command-line behavior in `AdoWorkItemTreeCloner`.
- Add or update tests for behavior changes.
- Keep package versions in `Directory.Packages.props`.
- Keep formatting/naming rules in `.editorconfig`.
- Do not weaken analyzer rules merely to hide a new warning.
- Use narrow, documented suppressions only when a rule genuinely does not apply.

## Security

Never commit PATs, Authorization headers, production Azure DevOps traces containing credentials.
