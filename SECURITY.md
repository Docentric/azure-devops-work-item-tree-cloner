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

`.gitignore` excludes `.env` files. `.env.example` contains only a placeholder.

Do not commit:

- PAT values.
- Authorization headers.
- HTTP traces containing secrets.
- Azure DevOps service connection credentials.

## Dependency security

NuGet auditing is enabled for all projects through `Directory.Build.props`. Warnings are treated as errors, so known vulnerable dependency findings can fail the build.
