namespace AdoWorkItemTreeCloner.Core.AzureDevOps;

/// <summary>
/// Represents an Azure DevOps REST API failure.
/// </summary>
public sealed class AzureDevOpsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDevOpsException"/> class.
    /// </summary>
    public AzureDevOpsException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDevOpsException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public AzureDevOpsException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDevOpsException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public AzureDevOpsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
