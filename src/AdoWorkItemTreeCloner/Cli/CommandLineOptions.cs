namespace AdoWorkItemTreeCloner.Cli;

/// <summary>
/// Strongly typed, validated representation of the parsed command-line arguments.
/// </summary>
internal sealed record CommandLineOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLineOptions"/> class.
    /// </summary>
    /// <param name="organization">Azure DevOps organization URL.</param>
    /// <param name="project">Source and target Azure DevOps project.</param>
    /// <param name="rootId">ID of the source root work item to clone.</param>
    /// <param name="personalAccessToken">Azure DevOps personal access token.</param>
    /// <param name="titleSuffix">Suffix appended only to the cloned root title.</param>
    /// <param name="dryRun">Whether to only read and display the tree without creating work items.</param>
    /// <param name="copyAreaPath">Whether to copy <c>System.AreaPath</c>.</param>
    /// <param name="copyIterationPath">Whether to copy <c>System.IterationPath</c>.</param>
    /// <param name="copyAssignedTo">Whether to copy <c>System.AssignedTo</c>.</param>
    /// <param name="suppressNotifications">Whether Azure DevOps notifications are suppressed on writes.</param>
    /// <param name="newParentId">
    /// Optional ID of an existing Azure DevOps work item that should become the parent of the newly
    /// created root work item. When <see langword="null"/>, the cloned root is created without a parent.
    /// </param>
    public CommandLineOptions(
        string organization,
        string project,
        int rootId,
        string personalAccessToken,
        string titleSuffix,
        bool dryRun,
        bool copyAreaPath,
        bool copyIterationPath,
        bool copyAssignedTo,
        bool suppressNotifications,
        int? newParentId = null)
    {
        Organization = organization;
        Project = project;
        RootId = rootId;
        PersonalAccessToken = personalAccessToken;
        TitleSuffix = titleSuffix;
        DryRun = dryRun;
        CopyAreaPath = copyAreaPath;
        CopyIterationPath = copyIterationPath;
        CopyAssignedTo = copyAssignedTo;
        SuppressNotifications = suppressNotifications;
        NewParentId = newParentId;
    }

    /// <summary>
    /// Gets the Azure DevOps organization URL.
    /// </summary>
    public string Organization { get; }

    /// <summary>
    /// Gets the source and target Azure DevOps project.
    /// </summary>
    public string Project { get; }

    /// <summary>
    /// Gets the ID of the source root work item to clone.
    /// </summary>
    public int RootId { get; }

    /// <summary>
    /// Gets the Azure DevOps personal access token.
    /// </summary>
    public string PersonalAccessToken { get; }

    /// <summary>
    /// Gets the suffix appended only to the cloned root title.
    /// </summary>
    public string TitleSuffix { get; }

    /// <summary>
    /// Gets a value indicating whether the tree should only be read and displayed, without creating work items.
    /// </summary>
    public bool DryRun { get; }

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
    /// Gets a value indicating whether Azure DevOps write notifications are suppressed.
    /// </summary>
    public bool SuppressNotifications { get; }

    /// <summary>
    /// Gets the optional ID of an existing Azure DevOps work item that should become the parent of the
    /// newly created root work item. <see langword="null"/> means the cloned root is created without a parent.
    /// </summary>
    public int? NewParentId { get; }
}
