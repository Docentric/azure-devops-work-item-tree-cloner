# Field-copy policy

Azure DevOps work items contain business fields, process metadata, lifecycle fields, history fields, identity values, and board-order fields. Copying every field verbatim is not safe because many values are read-only or server-managed.

`WorkItemCreatePatchBuilder` therefore applies an explicit policy.

## Always considered for copy

The following `System.*` fields are allowed:

- `System.Title`.
- `System.Description`.
- `System.Tags`.

The root title receives `--title-suffix`; descendant titles remain unchanged.

## Optional system fields

### Area Path

Copied by default:

```text
System.AreaPath
```

Use `--reset-area-path` to let Azure DevOps apply its normal target default.

### Iteration Path

Not copied by default:

```text
System.IterationPath
```

Use `--copy-iteration-path` to copy it.

### Assigned To

Not copied by default:

```text
System.AssignedTo
```

Use `--copy-assigned-to` to copy it.

When Azure DevOps returns an identity object, the cloner normalizes it to its `uniqueName` value when available.

## Excluded fields

The application excludes known lifecycle/server fields, including:

- IDs and revisions.
- State and Reason.
- Created/Changed/Authorized metadata.
- Work item type/project metadata.
- Board column/lane data.
- activation/resolution/closure timestamps and identities.
- backlog ordering fields such as Stack Rank and Backlog Priority.

The complete list is in `WorkItemCreatePatchBuilder.ExcludedFields`.

## Microsoft.VSTS and custom fields

Non-excluded fields outside `System.*` are copied by default. This includes many `Microsoft.VSTS.*` business fields and custom process fields.

This is intentionally pragmatic for template-like work item trees, but Azure DevOps process rules remain authoritative. A clone can fail if a copied field is:

- read-only;
- absent from the target work item type;
- constrained to allowed values that no longer include the source value;
- backed by an identity that no longer exists;
- otherwise rejected by process rules.

## Why state is reset

Cloning directly into an arbitrary source state can violate process transitions, required-field rules, or state-specific behavior. New work items therefore use the target process's normal initial state.
