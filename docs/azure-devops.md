# Azure DevOps integration

## Required permission

Use a PAT with **Work Items: Read & write**. The corresponding REST/OAuth scope is `vso.work_write`.

This scope permits reading, creating, and updating work items and related work-item metadata.

Microsoft reference:

- <https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/work-items/create?view=azure-devops-rest-7.1>
- <https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/work-items/update?view=azure-devops-rest-7.1>

## Authentication

The client uses Azure DevOps PAT Basic authentication:

```text
Authorization: Basic base64(":" + PAT)
```

The PAT is never written to normal console output.

Prefer `AZURE_DEVOPS_PAT` over `--pat` because process arguments may be visible in shell history or process inspection tools.

## APIs used

### Read work item

```http
GET {organization}/{project}/_apis/wit/workitems/{id}?$expand=relations&api-version=7.1
```

### Create work item

```http
POST {organization}/{project}/_apis/wit/workitems/${type}?suppressNotifications=true&api-version=7.1
Content-Type: application/json-patch+json
```

### Add parent link

```http
PATCH {organization}/{project}/_apis/wit/workitems/{childId}?suppressNotifications=true&api-version=7.1
Content-Type: application/json-patch+json
```

Relation:

```text
System.LinkTypes.Hierarchy-Reverse
```

## Notifications

Writes suppress Azure DevOps notifications by default. Use `--notify` to allow normal notification behavior.

## Process rules

The source and target are currently the same Azure DevOps project. The target project process validates all copied fields.

Typical validation failures are:

- A custom field does not exist on a work item type.
- A field is read-only.
- A value violates an allowed-values rule.
- An identity no longer exists.
- Area or Iteration Path is unavailable.

Start with `--dry-run`, then clone a small representative hierarchy before using the tool on a large template tree.
