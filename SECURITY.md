# Security

## Personal access tokens

The application needs a PAT with Azure DevOps Work Items read/write permission.

Use the least privilege and shortest practical expiration period.

Prefer:

```text
AZURE_DEVOPS_PAT
```

over `--pat` because command-line arguments can be retained in shell history or exposed through process inspection.

The application does not intentionally print the PAT or Authorization header.

## Repository hygiene

The application does not read PAT values from `.env` files; the PAT must be supplied via the `AZURE_DEVOPS_PAT` environment variable or the `--pat` argument.

Do not commit:

- PAT values.
- Authorization headers.
- HTTP traces containing secrets.
- Azure DevOps service connection credentials.

## Dependency security

NuGet auditing is enabled for all projects through `Directory.Build.props`. Warnings are treated as errors, so known vulnerable dependency findings can fail the build.
