# Changelog

## 1.0.0 - 2026-09-07

- Initial complete .NET 10 / C# 14 solution.
- Recursive Azure DevOps Parent/Child tree cloning.
- Dry-run mode.
- Source → cloned work item ID mapping.
- Configurable Area Path, Iteration Path, Assigned To, and notifications, plus a `--newparentid` option to link a cloned root work item to a new parent.
- Preserve non-hierarchy relations during work item cloning, with support for relation names.
- Attachment copy support during work item cloning.
- Revert logic for partial clone failures and delete API support.
- Copy backlog ordering fields by default in clone patch and sort child work items by backlog order.
- CLI UX with banner, metadata, options table, interactive prompts for missing options, and clickable Azure DevOps links in output.
- SDK-style `.slnx` repository.
- Central Package Management.
- .NET analyzers, StyleCop, `.editorconfig`, warnings as errors.
- xUnit v3 `4.0.0` on Microsoft Testing Platform v2.
- TRX results and Microsoft Cobertura code coverage.
- Azure DevOps YAML build pipeline and ZIP artifact.
- CI and Release GitHub Actions workflows.
- Assembly signing (`AdoWorkItemTreeCloner.snk`).
- Cross-platform engineering build scripts.
- Architecture, Azure DevOps, field-copy, testing, CI/CD, troubleshooting, security, and contribution documentation.
