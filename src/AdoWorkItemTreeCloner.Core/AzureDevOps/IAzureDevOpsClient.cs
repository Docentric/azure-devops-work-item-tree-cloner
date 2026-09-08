using System.Text.Json.Nodes;

namespace AdoWorkItemTreeCloner.Core.AzureDevOps;

/// <summary>
/// Provides the Azure DevOps Work Item Tracking operations required by the cloner.
/// </summary>
public interface IAzureDevOpsClient
{
    /// <summary>
    /// Gets a work item with its relations expanded.
    /// </summary>
    /// <param name="id">Work item ID.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The Azure DevOps work item JSON.</returns>
    Task<JsonObject> GetWorkItemAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a work item using JSON Patch operations.
    /// </summary>
    /// <param name="workItemType">Azure DevOps work item type.</param>
    /// <param name="patchOperations">JSON Patch operations for fields to set.</param>
    /// <param name="suppressNotifications">Whether Azure DevOps notifications are suppressed.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The ID assigned to the created work item.</returns>
    Task<int> CreateWorkItemAsync(
        string workItemType,
        IReadOnlyList<JsonObject> patchOperations,
        bool suppressNotifications,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds a parent relation to a work item.
    /// </summary>
    /// <param name="childId">ID of the child work item.</param>
    /// <param name="parentId">ID of the parent work item.</param>
    /// <param name="suppressNotifications">Whether Azure DevOps notifications are suppressed.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous update.</returns>
    Task AddParentRelationAsync(
        int childId,
        int parentId,
        bool suppressNotifications,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the binary content of an attachment.
    /// </summary>
    /// <param name="attachmentUrl">Absolute attachment content URL, typically from a work item relation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The attachment's binary content.</returns>
    Task<byte[]> DownloadAttachmentAsync(Uri attachmentUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads binary content as a new Azure DevOps attachment.
    /// </summary>
    /// <param name="fileName">File name associated with the attachment.</param>
    /// <param name="content">Binary content to upload.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The absolute URL of the newly created attachment.</returns>
    Task<string> UploadAttachmentAsync(string fileName, byte[] content, CancellationToken cancellationToken);

    /// <summary>
    /// Adds an attachment relation to a work item.
    /// </summary>
    /// <param name="workItemId">ID of the work item receiving the attachment.</param>
    /// <param name="attachmentUrl">Absolute URL of an existing attachment.</param>
    /// <param name="comment">Optional comment describing the attachment relation.</param>
    /// <param name="suppressNotifications">Whether Azure DevOps notifications are suppressed.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous update.</returns>
    Task AddAttachmentRelationAsync(
        int workItemId,
        string attachmentUrl,
        string? comment,
        bool suppressNotifications,
        CancellationToken cancellationToken);
}
