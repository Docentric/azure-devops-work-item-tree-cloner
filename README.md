# Azure DevOps Work Item Tree Cloner

[![CI](https://github.com/Docentric/azure-devops-work-item-tree-cloner/actions/workflows/ci.yml/badge.svg)](https://github.com/Docentric/azure-devops-work-item-tree-cloner/actions/workflows/ci.yml)
[![Release](https://github.com/Docentric/azure-devops-work-item-tree-cloner/actions/workflows/release.yml/badge.svg)](https://github.com/Docentric/azure-devops-work-item-tree-cloner/actions/workflows/release.yml)
[![Coverage (CI)](https://raw.githubusercontent.com/Docentric/azure-devops-work-item-tree-cloner/badges/ci-coverage.svg)](https://github.com/Docentric/azure-devops-work-item-tree-cloner/actions/workflows/ci.yml)
[![Coverage (Release)](https://raw.githubusercontent.com/Docentric/azure-devops-work-item-tree-cloner/badges/release-coverage.svg)](https://github.com/Docentric/azure-devops-work-item-tree-cloner/actions/workflows/release.yml)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/Docentric/azure-devops-work-item-tree-cloner)](LICENSE)
[![GitHub release](https://img.shields.io/github/v/release/Docentric/azure-devops-work-item-tree-cloner)](https://github.com/Docentric/azure-devops-work-item-tree-cloner/releases/latest)

A command-line tool for cloning Azure DevOps work item trees (an epic/feature/backlog item/task hierarchy) into new,
independent copies while preserving the parent/child structure. It was originally built to speed up Docentric's own
development workflow — cloning templates such as onboarding checklists and other repeating project structures — but
it is generic enough for anyone who needs to duplicate a work item tree in Azure DevOps. In particular, the community
is welcome to use it together with the
[D365 Business Process Catalog](https://learn.microsoft.com/en-us/dynamics365/fin-ops-core/dev-itpro/audit/business-processes/business-process-catalog)
to clone standard process templates into new Azure DevOps projects.

The repository is deliberately structured

## What it clones

Given:

```text
Epic
├── Feature A
│   ├── Product Backlog Item 1
│   │   ├── Task 1
│   │   └── Task 2
│   └── Product Backlog Item 2
└── Feature B
    └── Product Backlog Item 3
```

it creates:

```text
Epic - Copy
├── Feature A
│   ├── Product Backlog Item 1
│   │   ├── Task 1
│   │   └── Task 2
│   └── Product Backlog Item 2
└── Feature B
    └── Product Backlog Item 3
```

The application maintains a source ID → cloned ID map and recreates the `Parent` relation for every descendant.

## Technology baseline

| Component | Choice |
|---|---|
| .NET SDK | `10.0.400`, pinned in `global.json` |
| Target framework | `net10.0` |
| C# | `14.0` |
| Solution format | `.slnx` |
| CLI | `System.CommandLine` |
| Console UI | `Spectre.Console` |
| Test framework | xUnit v3 `4.0.0` |
| Test platform | Microsoft Testing Platform v2 |
| Coverage | `Microsoft.Testing.Extensions.CodeCoverage` |
| TRX | `Microsoft.Testing.Extensions.TrxReport` |
| Style | built-in .NET analyzers + `NewStyleCop.Analyzers` + `.editorconfig` |

Package versions are centralized in `Directory.Packages.props`.

## Repository structure

```text
.
├── .azuredevops/
│   └── pull_request_template.md
├── AdoWorkItemTreeCloner.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── .editorconfig
├── stylecop.json
├── azure-pipelines.yml
├── src/
│   ├── AdoWorkItemTreeCloner/
│   │   ├── Cli/
│   │   └── Program.cs
│   └── AdoWorkItemTreeCloner.Core/
│       ├── AzureDevOps/
│       └── Cloning/
├── tests/
│   └── AdoWorkItemTreeCloner.Core.Tests/
├── eng/
│   ├── build.ps1
│   └── build.sh
└── docs/
    ├── architecture.md
    ├── azure-devops.md
    ├── ci-cd.md
    ├── development.md
    ├── field-copy-policy.md
    ├── testing.md
    └── troubleshooting.md
```

## Requirements

- .NET SDK `10.0.400` or a compatible patch allowed by `global.json`.
- Azure DevOps project access.
- A PAT with **Work Items: Read & write** (`vso.work_write`).

## Authentication

Prefer an environment variable so the PAT is not stored in shell history.

### PowerShell

```powershell
$env:AZURE_DEVOPS_PAT = '<PAT>'
```

### cmd.exe

```cmd
set AZURE_DEVOPS_PAT=<PAT>
```

### Bash

```bash
export AZURE_DEVOPS_PAT='<PAT>'
```

`--pat` is supported as a fallback, but is not recommended for normal use.

## First run: dry run

Always inspect the tree before writing:

```powershell
dotnet run --project src/AdoWorkItemTreeCloner -- `
  --organization "https://dev.azure.com/docentric" `
  --project "DocentricAX7_DEV" `
  --root 15205 `
  --dry-run
```

A dry run performs all reads, recursively builds the source tree, validates that it is a tree, and renders it. It creates nothing.

## Clone

```powershell
dotnet run --project src/AdoWorkItemTreeCloner -- `
  --organization "https://dev.azure.com/docentric" `
  --project "DocentricAX7_DEV" `
  --root 15205 `
  --title-suffix " - Copy"
```

Example result:

```text
Clone completed
New root work item: 16120
Work items cloned: 6
Parent/Child links created: 5

Source ID  New ID
15205      16120
15206      16121
15210      16122
...
```

To attach the newly cloned root under an existing work item, pass `--newparentid`:

```powershell
dotnet run --project src/AdoWorkItemTreeCloner -- `
  --organization "https://dev.azure.com/docentric" `
  --project "DocentricAX7_DEV" `
  --root 15205 `
  --title-suffix " - Copy" `
  --newparentid 14000
```

When `--newparentid` is provided, a Parent/Child relation is created between the specified work item and the newly cloned root, in addition to the relations recreated within the cloned tree. The relation count reported at the end includes this extra link.

## CLI options

| Option | Description | Default |
|---|---|---|
| `--organization` | Azure DevOps organization URL. | Required |
| `--project` | Source and target Azure DevOps project. | Required |
| `--root` | Source root work item ID. | Required |
| `--pat` | PAT. Prefer `AZURE_DEVOPS_PAT`. | Environment variable |
| `--title-suffix` | Suffix applied only to the cloned root title. | ` - Copy` |
| `--dry-run` | Read/render only. | `false` |
| `--reset-area-path` | Do not copy `System.AreaPath`. | Area Path copied |
| `--copy-iteration-path` | Copy `System.IterationPath`. | `false` |
| `--copy-assigned-to` | Copy `System.AssignedTo`. | `false` |
| `--no-copy-attachments` | Do not copy attachments. | Attachments copied |
| `--notify` | Allow Azure DevOps notifications for created/updated work items. | Notifications suppressed |
| `--newparentid` | ID of an existing work item that becomes the parent of the newly created root work item. | No parent link created |

`System.CommandLine` also provides `--help` and `--version`.

## Field-copy behavior

The cloner copies normal business/custom data but intentionally avoids fields owned by Azure DevOps lifecycle, history, board ordering, or identity metadata.

Typical copied fields include:

- `System.Title`.
- `System.Description`.
- `System.Tags`.
- `System.AreaPath`, unless reset.
- `System.IterationPath`, when enabled.
- `System.AssignedTo`, when enabled.
- `Microsoft.VSTS.*` business fields such as Priority, except explicitly excluded lifecycle/order fields.
- Custom process fields.

The root title receives the configured suffix. Descendant titles are preserved.

See [Field-copy policy](docs/field-copy-policy.md).

## What is intentionally not cloned

- Revision/history data.
- Discussion/comments.
- State transition history.

Attachments are copied by default (use `--no-copy-attachments` to skip them), and non-hierarchy relations (Related, Predecessor/Successor, Hyperlinks, Git/build/branch/pull-request artifact links, etc.) are preserved and remapped to cloned targets when applicable. Backlog ordering fields (Stack Rank, Backlog Priority) are also copied by default so children keep their relative order.

The tool focuses on cloning the **complete recursive Parent/Child tree structure**, including its non-hierarchy relations and attachments.

## Build and quality gates

PowerShell:

```powershell
./eng/build.ps1
```

Bash:

```bash
./eng/build.sh
```

The engineering build performs:

1. `dotnet restore`.
2. `dotnet format --verify-no-changes`.
3. Release build with warnings as errors.
4. xUnit v3 tests through Microsoft Testing Platform v2.
5. TRX result generation.
6. Cobertura code coverage generation.

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

## Tests

Unit tests do **not** require Azure DevOps credentials and do not modify any external system.

They cover:

- Recursive hierarchy loading.
- Arbitrary depth.
- Child-only relation traversal.
- Cycle detection.
- Duplicate-node detection.
- Source → clone ID mapping.
- Parent relation recreation.
- Root-only title suffix behavior.
- Field filtering.
- Area/Iteration/Assigned To options.
- Identity-field normalization.
- REST request URI construction.
- PAT Basic authentication.
- JSON Patch media type and payloads.
- Organization-scoped work item relation URLs.

See [Testing](docs/testing.md).

## Azure DevOps pipeline

`azure-pipelines.yml`:

1. Installs the SDK from `global.json`.
2. Caches NuGet packages.
3. Restores dependencies.
4. Verifies formatting.
5. Builds Release.
6. Runs tests on Microsoft Testing Platform v2.
7. Publishes TRX results.
8. Publishes Cobertura code coverage.
9. Publishes the CLI.
10. Creates a ZIP build artifact.

No PAT is required by CI because the included tests are isolated unit tests.

See [CI/CD](docs/ci-cd.md).

## Important transactional limitation

Azure DevOps does not offer a transaction across a set of work-item creates and relation updates. If a request fails halfway through a clone, the new hierarchy can be partial.

The application intentionally does not automatically delete already-created work items. Automatic rollback would be destructive and could remove useful diagnostic state. Use `--dry-run` and test with a small representative tree first.

## Documentation

- [Architecture](docs/architecture.md)
- [Azure DevOps integration](docs/azure-devops.md)
- [Field-copy policy](docs/field-copy-policy.md)
- [Development standards](docs/development.md)
- [Testing](docs/testing.md)
- [CI/CD](docs/ci-cd.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)
