namespace AdoWorkItemTreeCloner.Core.Cloning;

/// <summary>
/// Wraps a failure or cancellation that occurred mid-clone, carrying the partial <see cref="CloneResult"/> so
/// callers can revert (delete) any work items that were already created before the failure.
/// </summary>
public sealed class CloneAbortedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CloneAbortedException"/> class.
    /// </summary>
    /// <param name="innerException">The exception that aborted the clone.</param>
    /// <param name="partialResult">Mapping and relation statistics accumulated before the failure.</param>
    public CloneAbortedException(Exception innerException, CloneResult partialResult)
        : base(innerException?.Message ?? "The clone operation was aborted.", innerException)
    {
        PartialResult = partialResult ?? throw new ArgumentNullException(nameof(partialResult));
    }

    /// <summary>
    /// Gets the work items and relations that were created before the clone was aborted.
    /// </summary>
    public CloneResult PartialResult { get; }
}
