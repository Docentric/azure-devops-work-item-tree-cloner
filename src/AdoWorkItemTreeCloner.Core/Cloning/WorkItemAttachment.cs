namespace AdoWorkItemTreeCloner.Core.Cloning;

/// <summary>
/// Represents a source work item attachment relation to be recreated on the cloned work item.
/// </summary>
/// <param name="Url">Absolute URL of the source attachment content.</param>
/// <param name="FileName">Original file name of the attachment, when known.</param>
/// <param name="Comment">Optional comment associated with the attachment relation.</param>
public sealed record WorkItemAttachment(string Url, string? FileName, string? Comment);
