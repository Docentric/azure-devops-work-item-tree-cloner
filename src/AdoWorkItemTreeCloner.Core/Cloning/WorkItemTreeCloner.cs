using System.Globalization;
using System.Text.Json.Nodes;

using AdoWorkItemTreeCloner.Core.AzureDevOps;

namespace AdoWorkItemTreeCloner.Core.Cloning;

/// <summary>
/// Loads and recursively clones an Azure DevOps Parent/Child work item tree.
/// </summary>
public sealed class WorkItemTreeCloner
{
    private const string ChildRelation = "System.LinkTypes.Hierarchy-Forward";
    private const string ParentRelation = "System.LinkTypes.Hierarchy-Reverse";
    private const string AttachmentRelation = "AttachedFile";

    private readonly IAzureDevOpsClient _client;
    private readonly CloneOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkItemTreeCloner"/> class.
    /// </summary>
    /// <param name="client">Azure DevOps client used for work item operations.</param>
    /// <param name="options">Clone behavior and field-copy options.</param>
    public WorkItemTreeCloner(IAzureDevOpsClient client, CloneOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Loads the complete Parent/Child hierarchy rooted at <paramref name="rootId"/>.
    /// </summary>
    /// <param name="rootId">ID of the source root work item.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The fully materialized source hierarchy.</returns>
    public Task<WorkItemNode> LoadTreeAsync(int rootId, CancellationToken cancellationToken)
    {
        if (rootId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rootId),
                rootId,
                "Root work item ID must be greater than zero.");
        }

        HashSet<int> path = [];
        HashSet<int> seen = [];
        return LoadNodeAsync(rootId, path, seen, cancellationToken);
    }

    /// <summary>
    /// Clones all work items and recreates their Parent/Child hierarchy.
    /// </summary>
    /// <param name="sourceRoot">Root of the already loaded source hierarchy.</param>
    /// <param name="newParentId">
    /// Optional ID of an existing Azure DevOps work item that becomes the parent of the newly created root
    /// work item. When <see langword="null"/>, the cloned root is created without a parent.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Mapping and relation statistics for the cloned hierarchy.</returns>
    /// <exception cref="CloneAbortedException">
    /// The clone failed or was canceled partway through. The exception carries the partial
    /// <see cref="CloneResult"/> so the caller can revert any work items already created.
    /// </exception>
    public async Task<CloneResult> CloneAsync(
        WorkItemNode sourceRoot,
        int? newParentId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceRoot);

        var result = new CloneResult();

        try
        {
            result.RootNewId = await CloneNodeAsync(
                    sourceRoot,
                    parentNewId: null,
                    isRoot: true,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);

            if (newParentId.HasValue)
            {
                await _client.AddParentRelationAsync(
                        result.RootNewId,
                        newParentId.Value,
                        _options.SuppressNotifications,
                        cancellationToken)
                    .ConfigureAwait(false);
                result.RelationCount++;
            }

            await RecreateOtherRelationsAsync(sourceRoot, result, cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (Exception exception) when (result.IdMap.Count > 0)
        {
            throw new CloneAbortedException(exception, result);
        }
    }

    /// <summary>
    /// Recreates every node's non-hierarchy relations on its cloned counterpart, remapping the target to the
    /// corresponding cloned work item when the target was itself part of the cloned tree, otherwise pointing
    /// at the original (un-cloned) target.
    /// </summary>
    private async Task RecreateOtherRelationsAsync(
        WorkItemNode source,
        CloneResult result,
        CancellationToken cancellationToken)
    {
        var newId = result.IdMap[source.Id];

        foreach (WorkItemRelation relation in source.OtherRelations)
        {
            int? remappedTargetId = relation.TargetId.HasValue &&
                result.IdMap.TryGetValue(relation.TargetId.Value, out var newTargetId)
                ? newTargetId
                : null;

            await _client.AddRelationAsync(
                    newId,
                    relation.RelationType,
                    remappedTargetId,
                    remappedTargetId.HasValue ? null : relation.Url,
                    relation.Comment,
                    relation.Name,
                    _options.SuppressNotifications,
                    cancellationToken)
                .ConfigureAwait(false);
            result.PreservedRelationCount++;
        }

        foreach (WorkItemNode child in source.Children)
        {
            await RecreateOtherRelationsAsync(child, result, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string? GetString(JsonObject fields, string name)
    {
        if (!fields.TryGetPropertyValue(name, out JsonNode? value) || value is null)
        {
            return null;
        }

        return value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static int? ParseWorkItemId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out Uri? relationUri))
        {
            return null;
        }

        var lastSegment = relationUri.Segments.LastOrDefault()?.Trim('/');
        return int.TryParse(
            lastSegment,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var id)
            ? id
            : null;
    }

    private async Task<WorkItemNode> LoadNodeAsync(
        int id,
        HashSet<int> path,
        HashSet<int> seen,
        CancellationToken cancellationToken)
    {
        if (!path.Add(id))
        {
            throw new AzureDevOpsException(
                $"Cycle detected in Parent/Child hierarchy at work item {id}.");
        }

        if (!seen.Add(id))
        {
            path.Remove(id);
            throw new AzureDevOpsException(
                $"Work item {id} occurs more than once in the hierarchy. The source is not a tree.");
        }

        try
        {
            JsonObject json = await _client.GetWorkItemAsync(id, cancellationToken)
                .ConfigureAwait(false);
            JsonObject fields = json["fields"]?.AsObject()
                ?? throw new AzureDevOpsException($"Work item {id} has no fields object.");

            var node = new WorkItemNode
            {
                Id = id,
                WorkItemType = GetString(fields, "System.WorkItemType") ?? "Unknown",
                Title = GetString(fields, "System.Title") ?? $"Work item {id}",
                Fields = (JsonObject)fields.DeepClone()
            };

            if (json["relations"] is not JsonArray relations)
            {
                return node;
            }

            foreach (JsonNode? relationNode in relations)
            {
                if (relationNode is not JsonObject relation)
                {
                    continue;
                }

                var rel = relation["rel"]?.GetValue<string>();

                if (string.Equals(rel, AttachmentRelation, StringComparison.Ordinal))
                {
                    var attachmentUrl = relation["url"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(attachmentUrl))
                    {
                        JsonObject? attributes = relation["attributes"]?.AsObject();
                        var fileName = attributes?["name"]?.GetValue<string>();
                        var comment = attributes?["comment"]?.GetValue<string>();
                        node.Attachments.Add(new WorkItemAttachment(attachmentUrl, fileName, comment));
                    }

                    continue;
                }

                if (string.Equals(rel, ParentRelation, StringComparison.Ordinal))
                {
                    // The link back to this node's own parent is recreated separately via
                    // AddParentRelationAsync; it must not be captured as an "other" relation.
                    continue;
                }

                if (!string.Equals(rel, ChildRelation, StringComparison.Ordinal))
                {
                    var relationUrl = relation["url"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(relationUrl))
                    {
                        continue;
                    }

                    JsonObject? relationAttributes = relation["attributes"]?.AsObject();
                    var relationComment = relationAttributes?["comment"]?.GetValue<string>();
                    var relationName = relationAttributes?["name"]?.GetValue<string>();
                    node.OtherRelations.Add(
                        new WorkItemRelation(rel, ParseWorkItemId(relationUrl), relationUrl, relationComment, relationName));

                    continue;
                }

                var childId = ParseWorkItemId(relation["url"]?.GetValue<string>());
                if (childId is null)
                {
                    continue;
                }

                node.Children.Add(
                    await LoadNodeAsync(childId.Value, path, seen, cancellationToken)
                        .ConfigureAwait(false));
            }

            return node;
        }
        finally
        {
            path.Remove(id);
        }
    }

    private async Task<int> CloneNodeAsync(
        WorkItemNode source,
        int? parentNewId,
        bool isRoot,
        CloneResult result,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<JsonObject> patch = WorkItemCreatePatchBuilder.Build(source, isRoot, _options);
        var newId = await _client.CreateWorkItemAsync(
                source.WorkItemType,
                patch,
                _options.SuppressNotifications,
                cancellationToken)
            .ConfigureAwait(false);

        result.AddMapping(source.Id, newId);

        if (parentNewId.HasValue)
        {
            await _client.AddParentRelationAsync(
                    newId,
                    parentNewId.Value,
                    _options.SuppressNotifications,
                    cancellationToken)
                .ConfigureAwait(false);
            result.RelationCount++;
        }

        if (_options.CopyAttachments)
        {
            foreach (WorkItemAttachment attachment in source.Attachments)
            {
                await CloneAttachmentAsync(newId, attachment, result, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        foreach (WorkItemNode child in source.Children)
        {
            _ = await CloneNodeAsync(
                    child,
                    newId,
                    isRoot: false,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return newId;
    }

    private async Task CloneAttachmentAsync(
        int newWorkItemId,
        WorkItemAttachment attachment,
        CloneResult result,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(attachment.Url, UriKind.Absolute, out Uri? attachmentUri))
        {
            return;
        }

        byte[] content = await _client.DownloadAttachmentAsync(attachmentUri, cancellationToken)
            .ConfigureAwait(false);

        var fileName = string.IsNullOrWhiteSpace(attachment.FileName)
            ? GetAttachmentFileName(attachment.Url)
            : attachment.FileName;
        var newAttachmentUrl = await _client.UploadAttachmentAsync(fileName, content, cancellationToken)
            .ConfigureAwait(false);

        await _client.AddAttachmentRelationAsync(
                newWorkItemId,
                newAttachmentUrl,
                attachment.Comment,
                _options.SuppressNotifications,
                cancellationToken)
            .ConfigureAwait(false);

        result.AttachmentCount++;
    }

    private static string GetAttachmentFileName(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 &&
                    string.Equals(parts[0], "fileName", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(parts[1]);
                }
            }
        }

        return "attachment";
    }
}
