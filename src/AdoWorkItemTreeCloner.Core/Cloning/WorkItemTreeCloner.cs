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

        HashSet<int> path = new HashSet<int>();
        HashSet<int> seen = new HashSet<int>();
        return LoadNodeAsync(rootId, path, seen, cancellationToken);
    }

    /// <summary>
    /// Clones all work items and recreates their Parent/Child hierarchy.
    /// </summary>
    /// <param name="sourceRoot">Root of the already loaded source hierarchy.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Mapping and relation statistics for the cloned hierarchy.</returns>
    public async Task<CloneResult> CloneAsync(
        WorkItemNode sourceRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceRoot);

        CloneResult result = new CloneResult();
        result.RootNewId = await CloneNodeAsync(
                sourceRoot,
                parentNewId: null,
                isRoot: true,
                result,
                cancellationToken)
            .ConfigureAwait(false);

        return result;
    }

    private static string? GetString(JsonObject fields, string name)
    {
        if (!fields.TryGetPropertyValue(name, out JsonNode? value) || value is null)
        {
            return null;
        }

        return value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string? text)
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

        string? lastSegment = relationUri.Segments.LastOrDefault()?.Trim('/');
        return int.TryParse(
            lastSegment,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int id)
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

            WorkItemNode node = new WorkItemNode
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
                if (relationNode is not JsonObject relation ||
                    !string.Equals(
                        relation["rel"]?.GetValue<string>(),
                        ChildRelation,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int? childId = ParseWorkItemId(relation["url"]?.GetValue<string>());
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
        int newId = await _client.CreateWorkItemAsync(
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
}
