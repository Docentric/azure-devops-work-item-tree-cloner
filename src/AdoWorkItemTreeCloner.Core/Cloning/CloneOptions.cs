namespace AdoWorkItemTreeCloner.Core.Cloning;

/// <summary>
/// Controls which source values are carried into cloned work items.
/// </summary>
public sealed record CloneOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CloneOptions"/> class.
    /// </summary>
    /// <param name="titleSuffix">Suffix appended to the cloned root title.</param>
    /// <param name="copyAreaPath">Whether to copy <c>System.AreaPath</c>.</param>
    /// <param name="copyIterationPath">Whether to copy <c>System.IterationPath</c>.</param>
    /// <param name="copyAssignedTo">Whether to copy <c>System.AssignedTo</c>.</param>
    /// <param name="copyAttachments">Whether to copy work item attachments.</param>
    /// <param name="suppressNotifications">Whether Azure DevOps notifications are suppressed on writes.</param>
    public CloneOptions(
        string titleSuffix,
        bool copyAreaPath,
        bool copyIterationPath,
        bool copyAssignedTo,
        bool copyAttachments,
        bool suppressNotifications)
    {
        TitleSuffix = titleSuffix;
        CopyAreaPath = copyAreaPath;
        CopyIterationPath = copyIterationPath;
        CopyAssignedTo = copyAssignedTo;
        CopyAttachments = copyAttachments;
        SuppressNotifications = suppressNotifications;
    }

    /// <summary>
    /// Gets the suffix applied to the cloned root title.
    /// </summary>
    public string TitleSuffix { get; }

    /// <summary>
    /// Gets a value indicating whether <c>System.AreaPath</c> is copied.
    /// </summary>
    public bool CopyAreaPath { get; }

    /// <summary>
    /// Gets a value indicating whether <c>System.IterationPath</c> is copied.
    /// </summary>
    public bool CopyIterationPath { get; }

    /// <summary>
    /// Gets a value indicating whether <c>System.AssignedTo</c> is copied.
    /// </summary>
    public bool CopyAssignedTo { get; }

    /// <summary>
    /// Gets a value indicating whether work item attachments are copied.
    /// </summary>
    public bool CopyAttachments { get; }

    /// <summary>
    /// Gets a value indicating whether Azure DevOps write notifications are suppressed.
    /// </summary>
    public bool SuppressNotifications { get; }
}
