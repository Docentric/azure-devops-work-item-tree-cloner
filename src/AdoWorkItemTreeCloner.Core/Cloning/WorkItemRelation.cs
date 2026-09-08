namespace AdoWorkItemTreeCloner.Core.Cloning;

/// <summary>
/// Represents a non-hierarchy, non-attachment relation captured from a source work item, to be
/// recreated on the corresponding cloned work item once cloning completes.
/// </summary>
/// <param name="RelationType">Azure DevOps relation type (the <c>rel</c> value), e.g. <c>System.LinkTypes.Related</c>.</param>
/// <param name="TargetId">
/// Work item ID the relation points to, when the target URL could be parsed as a work item reference.
/// <see langword="null"/> for relations to non-work-item artifacts (commits, pull requests, hyperlinks, etc.).
/// </param>
/// <param name="Url">Original absolute URL of the relation target, used verbatim when the target isn't remapped.</param>
/// <param name="Comment">Optional comment attached to the relation.</param>
/// <param name="Name">
/// Optional relation name. Azure DevOps requires this for artifact links (e.g. commit or build links); it is
/// otherwise typically absent.
/// </param>
public sealed record WorkItemRelation(string RelationType, int? TargetId, string Url, string? Comment, string? Name = null);
