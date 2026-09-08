namespace AdoWorkItemTreeCloner.Core.Cloning;

/// <summary>
/// Describes the work items and hierarchy links created by a clone operation.
/// </summary>
public sealed class CloneResult
{
    private readonly Dictionary<int, int> _idMap = [];

    /// <summary>
    /// Gets the source-to-clone work item ID mapping.
    /// </summary>
    public IReadOnlyDictionary<int, int> IdMap => _idMap;

    /// <summary>
    /// Gets the number of parent/child relations created.
    /// </summary>
    public int RelationCount { get; internal set; }

    /// <summary>
    /// Gets the number of attachments copied.
    /// </summary>
    public int AttachmentCount { get; internal set; }

    /// <summary>
    /// Gets the number of non-hierarchy relations (Related, Predecessor/Successor, artifact links, etc.)
    /// recreated on the cloned work items.
    /// </summary>
    public int PreservedRelationCount { get; internal set; }

    /// <summary>
    /// Gets the new root work item ID.
    /// </summary>
    public int RootNewId { get; internal set; }

    internal void AddMapping(int sourceId, int newId) => _idMap.Add(sourceId, newId);
}
