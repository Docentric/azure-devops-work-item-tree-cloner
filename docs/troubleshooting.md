# Troubleshooting

## `401 Unauthorized`

Check:

- `AZURE_DEVOPS_PAT` is set in the process running the application.
- The PAT has not expired.
- The PAT belongs to an identity with access to the organization/project.
- The PAT includes **Work Items: Read & write**.

Avoid printing the PAT while diagnosing the problem.

## `403 Forbidden`

The token can be valid but the identity may not have permission to edit work items in the project, Area Path, or Iteration Path.

## `400 Bad Request` during create

This is usually a process/field validation failure.

Common causes:

- A custom field is read-only.
- A field value is no longer allowed.
- `System.AssignedTo` references an unavailable identity.
- An Area or Iteration Path does not exist or is not accessible.
- A required field is not satisfied by the source values/defaults.

Start with `--dry-run`, then clone a small representative subtree.

## Partial clone after an error

Azure DevOps does not provide a transaction covering all work-item creates and relation updates. If a request fails after earlier items were created, those items remain.

The application does not auto-delete them. Review the console output and the new IDs before performing manual cleanup.

## Tree is rejected as cyclic

Azure DevOps Parent/Child relations are expected to form an acyclic hierarchy. The application rejects a cycle rather than recursing indefinitely.

## Tree is rejected because an item occurs twice

The cloner intentionally requires a true tree. A work item reached through two different Parent/Child paths would make clone-parent semantics ambiguous, so the source is rejected.

## Build fails on warnings

This repository intentionally treats analyzer, compiler, and code-style warnings as errors.

Run:

```bash
dotnet format AdoWorkItemTreeCloner.slnx
./eng/build.sh
```

Do not disable a rule globally just to make one warning disappear. Fix the code or add the narrowest justified suppression.
