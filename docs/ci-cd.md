# CI/CD

The `.github/workflows/ci.yml` workflow runs continuous integration on GitHub Actions.

## Triggers

The workflow runs for:

- pushes to `main` and `develop`;
- pull requests targeting `main` and `develop`.

## Build environment

The workflow uses `ubuntu-latest` and `actions/setup-dotnet` with a pinned `.NET` version.

## Pipeline flow

1. Checkout the repository.
2. Install the .NET SDK.
3. Restore NuGet packages.
4. Build Release.
5. Run xUnit v3 tests on Microsoft Testing Platform v2, generating TRX and Cobertura coverage.
6. Publish test results via `dorny/test-reporter`.
7. Publish a coverage summary/badge via `irongut/CodeCoverageSummary`.
8. Publish the coverage badge to the `badges` branch (on `main`/`develop`).
9. Upload test and coverage artifacts.

## Why restore is not `--locked-mode`

Central Package Management pins all direct package versions in `Directory.Packages.props`, but this repository does not ship generated `packages.lock.json` files.

Using `dotnet restore --locked-mode` without committed lock files would fail the build. If the team later decides to commit NuGet lock files, enable `RestorePackagesWithLockFile` and only then switch CI to `--locked-mode`.

## Tests do not need Azure DevOps credentials

The CI workflow does not require `AZURE_DEVOPS_PAT`. All included tests use fakes or an in-memory `HttpMessageHandler` and do not make live Azure DevOps calls.

If integration tests are added later, keep them in a separate project/job and use GitHub Actions secrets or an environment-scoped secret store.
