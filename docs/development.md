# Development

## SDK and language

The repository pins .NET SDK `10.0.400` in `global.json` and permits latest-patch roll-forward within that feature band.

All projects inherit these settings from `Directory.Build.props`:

```xml
<TargetFramework>net10.0</TargetFramework>
<LangVersion>14.0</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

C# is pinned to `14.0` rather than the floating `latest` value so future SDK installations cannot silently change the language version used by the repository.

Check the active SDK:

```bash
dotnet --version
```

## Solution format

The repository uses the modern XML-based `.slnx` solution format:

```text
AdoWorkItemTreeCloner.slnx
```

Use the `.slnx` file in Visual Studio 2026 or with `dotnet` CLI commands.

## Central Package Management

Package versions live only in `Directory.Packages.props`.

Project files use versionless references:

```xml
<PackageReference Include="Spectre.Console" />
```

Do not add package versions directly to individual project files unless Central Package Management is intentionally being removed.

## Static analysis

`Directory.Build.props` enables:

- `TreatWarningsAsErrors`.
- `CodeAnalysisTreatWarningsAsErrors`.
- .NET analyzers.
- `latest-recommended` analysis level.
- code-style enforcement during build.
- `NewStyleCop.Analyzers`.
- nullable reference types.
- NuGet vulnerability auditing.
- deterministic builds.

`.editorconfig` is the source of truth for formatting, naming, and analyzer-specific policy.

`stylecop.json` contains the StyleCop configuration shared by every C# project.

## Formatting

Verify formatting:

```bash
dotnet format AdoWorkItemTreeCloner.slnx --verify-no-changes
```

Apply formatting:

```bash
dotnet format AdoWorkItemTreeCloner.slnx
```

## Build

```bash
dotnet build AdoWorkItemTreeCloner.slnx -c Release
```

Or run the full engineering build:

```powershell
./eng/build.ps1
```

```bash
./eng/build.sh
```

## Design rules

- Keep Azure DevOps I/O behind `IAzureDevOpsClient`.
- Keep copy policy in `WorkItemCreatePatchBuilder`.
- Keep command-line and terminal rendering concerns in the executable project.
- Keep the core project independent of `Spectre.Console` and `System.CommandLine`.
- Add tests for every change in cloning semantics.
- Never log PAT values or Authorization headers.
- Prefer cancellation-aware asynchronous APIs.
- Keep the REST API surface minimal and explicit.
