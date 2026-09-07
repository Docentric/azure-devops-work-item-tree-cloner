# CI/CD

The root `azure-pipelines.yml` is designed for Azure DevOps Pipelines.

## Triggers

The pipeline runs for:

- pushes to `main`;
- pull requests targeting `main`.

## Build environment

The pipeline uses `ubuntu-latest` and `UseDotNet@2` with `useGlobalJson: true`.

This aligns CI with the SDK policy declared in the repository rather than relying on whichever SDK happens to be preinstalled on the hosted agent.

## Pipeline flow

1. Checkout with clean workspace and full Git history.
2. Install .NET SDK from `global.json`.
3. Restore/cache NuGet packages.
4. Run `dotnet format --verify-no-changes`.
5. Build Release with warnings as errors.
6. Run xUnit v3 tests on Microsoft Testing Platform v2.
7. Generate TRX test reports.
8. Generate Cobertura code coverage.
9. Publish test results.
10. Publish code coverage.
11. `dotnet publish` the console application.
12. Archive the published output into `AdoWorkItemTreeCloner.zip`.
13. Publish that ZIP as a pipeline artifact.

## Why restore is not `--locked-mode`

Central Package Management pins all direct package versions in `Directory.Packages.props`, but this repository does not ship generated `packages.lock.json` files.

Using `dotnet restore --locked-mode` without committed lock files would fail the build. If the team later decides to commit NuGet lock files, enable `RestorePackagesWithLockFile` and only then switch CI to `--locked-mode`.

## Tests do not need Azure DevOps credentials

The default pipeline does not require `AZURE_DEVOPS_PAT`. All included tests use fakes or an in-memory `HttpMessageHandler` and do not make live Azure DevOps calls.

If integration tests are added later, keep them in a separate project/stage and use Azure DevOps secret variables or a Key Vault-backed variable group.
