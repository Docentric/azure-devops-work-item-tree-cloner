namespace AdoWorkItemTreeCloner.Core.AzureDevOps;

/// <summary>
/// Configuration for the Azure DevOps REST API client.
/// </summary>
public sealed record AzureDevOpsClientOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDevOpsClientOptions"/> class.
    /// </summary>
    /// <param name="organization">Azure DevOps organization URL.</param>
    /// <param name="project">Azure DevOps project name or ID.</param>
    /// <param name="personalAccessToken">PAT used for Work Item Tracking REST API authentication.</param>
    public AzureDevOpsClientOptions(
        string organization,
        string project,
        string personalAccessToken)
    {
        Organization = organization;
        Project = project;
        PersonalAccessToken = personalAccessToken;
    }

    /// <summary>
    /// Gets the Azure DevOps organization URL.
    /// </summary>
    public string Organization { get; }

    /// <summary>
    /// Gets the Azure DevOps project name or ID.
    /// </summary>
    public string Project { get; }

    /// <summary>
    /// Gets the personal access token.
    /// </summary>
    public string PersonalAccessToken { get; }
}
