namespace AdoWorkItemTreeCloner.Cli;

internal sealed record CommandLineOptions
{
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
        bool suppressNotifications)
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
    }

    public string Organization { get; }

    public string Project { get; }

    public int RootId { get; }

    public string PersonalAccessToken { get; }

    public string TitleSuffix { get; }

    public bool DryRun { get; }

    public bool CopyAreaPath { get; }

    public bool CopyIterationPath { get; }

    public bool CopyAssignedTo { get; }

    public bool SuppressNotifications { get; }
}
