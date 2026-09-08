using System.Text.Json.Nodes;

namespace AdoWorkItemTreeCloner.Core.Cloning;

/// <summary>
/// Represents one source work item and its child hierarchy.
/// </summary>
public sealed class WorkItemNode
{
    /// <summary>
    /// Gets or initializes the source work item ID.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets or initializes the work item type.
    /// </summary>
    public required string WorkItemType { get; init; }

    /// <summary>
    /// Gets or initializes the work item title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or initializes a copy of the source fields.
    /// </summary>
    public required JsonObject Fields { get; init; }

    /// <summary>
    /// Gets the child work items.
    /// </summary>
    public List<WorkItemNode> Children { get; } = [];

    /// <summary>
    /// Gets the source work item's attachments.
    /// </summary>
    public List<WorkItemAttachment> Attachments { get; } = [];

    /// <summary>
    /// Gets the source work item's non-hierarchy, non-attachment relations (Related, Predecessor/Successor,
    /// artifact links, etc.), to be recreated on the cloned work item once the whole tree has been cloned.
    /// </summary>
    public List<WorkItemRelation> OtherRelations { get; } = [];

    /// <summary>
    /// Counts this node and all descendants.
    /// </summary>
    /// <returns>The number of nodes in this subtree, including this node.</returns>
    public int CountNodes() => 1 + Children.Sum(static child => child.CountNodes());
}
