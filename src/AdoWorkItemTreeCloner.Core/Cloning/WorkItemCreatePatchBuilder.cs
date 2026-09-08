using System.Text.Json.Nodes;

namespace AdoWorkItemTreeCloner.Core.Cloning;

/// <summary>
/// Builds the JSON Patch payload used to create cloned work items.
/// </summary>
public static class WorkItemCreatePatchBuilder
{
    private static readonly HashSet<string> _copyableSystemFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Title",
        "System.Description",
        "System.Tags"
    };

    private static readonly HashSet<string> _excludedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Id",
        "System.Rev",
        "System.State",
        "System.Reason",
        "System.CreatedDate",
        "System.CreatedBy",
        "System.ChangedDate",
        "System.ChangedBy",
        "System.AuthorizedDate",
        "System.AuthorizedAs",
        "System.Watermark",
        "System.CommentCount",
        "System.NodeName",
        "System.WorkItemType",
        "System.TeamProject",
        "System.BoardColumn",
        "System.BoardColumnDone",
        "System.BoardLane",
        "Microsoft.VSTS.Common.StateChangeDate",
        "Microsoft.VSTS.Common.ActivatedDate",
        "Microsoft.VSTS.Common.ActivatedBy",
        "Microsoft.VSTS.Common.ResolvedDate",
        "Microsoft.VSTS.Common.ResolvedBy",
        "Microsoft.VSTS.Common.ClosedDate",
        "Microsoft.VSTS.Common.ClosedBy",
        "Microsoft.VSTS.Common.CreatedDate",
        "Microsoft.VSTS.Common.CreatedBy",
        "Microsoft.VSTS.Common.ChangedDate",
        "Microsoft.VSTS.Common.ChangedBy"
    };

    /// <summary>
    /// Builds a work-item create patch from a source node.
    /// </summary>
    /// <param name="source">Source work item node.</param>
    /// <param name="isRoot">Whether the source node is the root of the cloned tree.</param>
    /// <param name="options">Field-copy and notification options.</param>
    /// <returns>JSON Patch operations suitable for Azure DevOps work item creation.</returns>
    public static IReadOnlyList<JsonObject> Build(
        WorkItemNode source,
        bool isRoot,
        CloneOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        List<JsonObject> operations = [];

        foreach ((var fieldName, JsonNode? value) in source.Fields)
        {
            if (value is null || !ShouldCopyField(fieldName, options))
            {
                continue;
            }

            JsonNode clonedValue = NormalizeFieldValue(value);
            if (fieldName.Equals("System.Title", StringComparison.OrdinalIgnoreCase) && isRoot)
            {
                clonedValue = JsonValue.Create($"{source.Title}{options.TitleSuffix}")!;
            }

            operations.Add(CreateFieldOperation(fieldName, clonedValue));
        }

        var hasTitle = operations.Any(static operation =>
            string.Equals(
                operation["path"]?.GetValue<string>(),
                "/fields/System.Title",
                StringComparison.Ordinal));

        if (!hasTitle)
        {
            operations.Insert(
                0,
                CreateFieldOperation(
                    "System.Title",
                    JsonValue.Create(isRoot ? $"{source.Title}{options.TitleSuffix}" : source.Title)!));
        }

        return operations;
    }

    private static JsonObject CreateFieldOperation(string fieldName, JsonNode value) =>
        new()
        {
            ["op"] = "add",
            ["path"] = $"/fields/{EscapeJsonPointer(fieldName)}",
            ["value"] = value
        };

    private static string EscapeJsonPointer(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static JsonNode NormalizeFieldValue(JsonNode value)
    {
        if (value is JsonObject identity &&
            identity["uniqueName"] is JsonValue uniqueNameValue &&
            uniqueNameValue.TryGetValue<string>(out var uniqueName) &&
            !string.IsNullOrWhiteSpace(uniqueName))
        {
            return JsonValue.Create(uniqueName)!;
        }

        return value.DeepClone();
    }

    private static bool ShouldCopyField(string fieldName, CloneOptions options)
    {
        if (_excludedFields.Contains(fieldName))
        {
            return false;
        }

        if (fieldName.Equals("System.AreaPath", StringComparison.OrdinalIgnoreCase))
        {
            return options.CopyAreaPath;
        }

        if (fieldName.Equals("System.IterationPath", StringComparison.OrdinalIgnoreCase))
        {
            return options.CopyIterationPath;
        }

        if (fieldName.Equals("System.AssignedTo", StringComparison.OrdinalIgnoreCase))
        {
            return options.CopyAssignedTo;
        }

        if (fieldName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
        {
            return _copyableSystemFields.Contains(fieldName);
        }

        return true;
    }
}
