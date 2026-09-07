# Testing

## Test stack

Tests target `net10.0` and use:

- `xunit.v3.mtp-v2` `4.0.0`.
- Microsoft Testing Platform v2.
- `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` for development-environment compatibility.
- `Microsoft.Testing.Extensions.TrxReport` for TRX output.
- `Microsoft.Testing.Extensions.CodeCoverage` for Microsoft code coverage.

The repository selects Microsoft Testing Platform globally in `global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

The test project also sets:

```xml
<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
```

This makes direct execution (`dotnet run` on the test project) use the MTP command-line experience as well.

## Run tests

```bash
dotnet test tests/AdoWorkItemTreeCloner.Core.Tests/AdoWorkItemTreeCloner.Core.Tests.csproj
```

## Run with TRX and coverage

```bash
dotnet test tests/AdoWorkItemTreeCloner.Core.Tests/AdoWorkItemTreeCloner.Core.Tests.csproj \
  --results-directory artifacts/TestResults \
  --report-trx \
  --report-trx-filename "{asm}_{tfm}_{arch}.trx" \
  --coverage \
  --coverage-output-format cobertura
```

Expected artifacts are written below `artifacts/TestResults`:

```text
*.trx
*.cobertura.xml
```

## Test strategy

### Core cloning tests

Use `FakeAzureDevOpsClient`. They exercise the clone algorithm without HTTP or credentials.

Covered scenarios include:

- Recursive loading.
- Arbitrary depth.
- Ignoring non-child relations.
- Cycle detection.
- Duplicate work item detection.
- Same work item types in the clone.
- Source → new ID mapping.
- Correct parent link IDs.
- Root-only title suffix.
- Field-copy options.

### REST client tests

Use a custom `HttpMessageHandler` to capture outgoing requests.

Covered scenarios include:

- PAT Basic Authorization header.
- Project/type URI escaping.
- JSON Patch content type.
- Notification suppression query parameter.
- `System.LinkTypes.Hierarchy-Reverse` payload.
- Organization-scoped relation target URL.

## Integration tests

No live Azure DevOps integration test is enabled by default. CI should not require a PAT and should not mutate a real project.

If live tests are introduced:

1. Put them in a separate integration-test project.
2. Use a dedicated Azure DevOps project.
3. Require an explicit opt-in environment variable.
4. Use a short-lived secret PAT or workload identity where appropriate.
5. Never run cleanup logic against arbitrary user work items.
