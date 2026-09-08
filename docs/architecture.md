# Architecture

## Goal

Clone an Azure DevOps work item Parent/Child tree with arbitrary depth while keeping the implementation small, testable, and independent from the Azure DevOps .NET SDK.

## Projects

### `AdoWorkItemTreeCloner`

The executable project owns command-line concerns:

- `System.CommandLine` option parsing.
- PAT/environment-variable binding.
- `Spectre.Console` progress, hierarchy rendering, and result tables.
- Process exit codes.

It references `AdoWorkItemTreeCloner.Core`.

### `AdoWorkItemTreeCloner.Core`

The core library owns Azure DevOps and cloning behavior:

- REST communication.
- Transient HTTP retry handling.
- Recursive hierarchy loading.
- Cycle/duplicate detection.
- Field-copy policy.
- Work item creation.
- Parent relation creation.
- Preservation of non-hierarchy relations (Related, Predecessor/Successor, artifact links, etc.), remapped to cloned targets when applicable.
- Source → clone ID mapping.

It has no third-party runtime dependencies.

### `AdoWorkItemTreeCloner.Core.Tests`

The test project uses xUnit v3. Most cloning tests use an in-memory fake `IAzureDevOpsClient`, so tests do not require network access or credentials.

## Clone algorithm

For each source node:

1. Load the work item with `$expand=relations`.
2. Follow only `System.LinkTypes.Hierarchy-Forward` relations to discover children.
3. Capture `AttachedFile` relations as attachments, and every other relation (excluding the node's own `System.LinkTypes.Hierarchy-Reverse` link to its parent) as an "other" relation to recreate later.
4. Detect cycles and duplicate nodes while loading.
5. Build a create JSON Patch using the source fields and copy policy.
6. Create the cloned work item with the same work item type.
7. Store `sourceId -> newId`.
8. When the node has a cloned parent, add a `System.LinkTypes.Hierarchy-Reverse` relation to that parent.
9. Recurse through all children.
10. Once the entire tree has been cloned (the full `sourceId -> newId` map is known), recreate every captured "other" relation on its cloned work item: if the relation's original target is itself part of the cloned tree, the new relation points at the corresponding cloned work item; otherwise it points at the original, un-cloned target.

The root title suffix is applied only in step 5 for the root node.

## Failure model

Azure DevOps does not provide a transaction spanning multiple work items. A failure after one or more creates can therefore leave a partial clone.

The application deliberately does **not** auto-delete work items on failure because deletion is destructive and could hide useful diagnostic state. The console prints the Azure DevOps error immediately. Use `--dry-run` first and validate custom field/process compatibility before cloning large trees.

## REST API

The client uses Azure DevOps Work Item Tracking REST API `7.1` directly.

Benefits:

- Small dependency surface.
- Full control over JSON Patch.
- Easy unit testing with `HttpMessageHandler`.
- No dependency on Azure DevOps SDK release cadence.

## Retry policy

The REST client retries up to four attempts for:

- HTTP 408.
- HTTP 429.
- HTTP 500.
- HTTP 502.
- HTTP 503.
- HTTP 504.

`Retry-After` is honored when supplied. Otherwise, a short exponential delay is used.
