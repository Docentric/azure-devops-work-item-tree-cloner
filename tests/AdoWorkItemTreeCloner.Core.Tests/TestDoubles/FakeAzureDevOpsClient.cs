using System.Text.Json.Nodes;

using AdoWorkItemTreeCloner.Core.AzureDevOps;

namespace AdoWorkItemTreeCloner.Core.Tests.TestDoubles;

internal sealed class FakeAzureDevOpsClient : IAzureDevOpsClient
{
    private int _nextId = 1000;

    public Dictionary<int, JsonObject> WorkItems { get; } = [];

    public List<CreateCall> CreateCalls { get; } = [];

    public List<ParentLinkCall> ParentLinkCalls { get; } = [];

    public Dictionary<string, byte[]> AttachmentContents { get; } = [];

    public List<UploadAttachmentCall> UploadAttachmentCalls { get; } = [];

    public List<AttachmentRelationCall> AttachmentRelationCalls { get; } = [];

    public List<RelationCall> RelationCalls { get; } = [];

    public List<int> DeletedWorkItemIds { get; } = [];

    public Task<JsonObject> GetWorkItemAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!WorkItems.TryGetValue(id, out JsonObject? workItem))
        {
            throw new InvalidOperationException($"Work item {id} is not configured in the fake client.");
        }

        return Task.FromResult((JsonObject)workItem.DeepClone());
    }

    public Task<int> CreateWorkItemAsync(
        string workItemType,
        IReadOnlyList<JsonObject> patchOperations,
        bool suppressNotifications,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var newId = _nextId++;

        CreateCalls.Add(
            new CreateCall(
                newId,
                workItemType,
                [.. patchOperations.Select(static item => (JsonObject)item.DeepClone())],
                suppressNotifications));

        return Task.FromResult(newId);
    }

    public Task AddParentRelationAsync(
        int childId,
        int parentId,
        bool suppressNotifications,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ParentLinkCalls.Add(new ParentLinkCall(childId, parentId, suppressNotifications));
        return Task.CompletedTask;
    }

    public Task<byte[]> DownloadAttachmentAsync(Uri attachmentUrl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!AttachmentContents.TryGetValue(attachmentUrl.AbsoluteUri, out var content))
        {
            throw new InvalidOperationException(
                $"Attachment {attachmentUrl} is not configured in the fake client.");
        }

        return Task.FromResult(content);
    }

    public Task<string> UploadAttachmentAsync(string fileName, byte[] content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = $"https://fake.example/_apis/wit/attachments/{Guid.NewGuid()}?fileName={fileName}";
        UploadAttachmentCalls.Add(new UploadAttachmentCall(fileName, content, url));
        return Task.FromResult(url);
    }

    public Task AddAttachmentRelationAsync(
        int workItemId,
        string attachmentUrl,
        string? comment,
        bool suppressNotifications,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AttachmentRelationCalls.Add(
            new AttachmentRelationCall(workItemId, attachmentUrl, comment, suppressNotifications));
        return Task.CompletedTask;
    }

    public Task AddRelationAsync(
        int workItemId,
        string relationType,
        int? targetWorkItemId,
        string? targetUrl,
        string? comment,
        string? name,
        bool suppressNotifications,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RelationCalls.Add(
            new RelationCall(workItemId, relationType, targetWorkItemId, targetUrl, comment, name, suppressNotifications));
        return Task.CompletedTask;
    }

    public Task DeleteWorkItemAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeletedWorkItemIds.Add(id);
        return Task.CompletedTask;
    }

    internal sealed class CreateCall
    {
        public CreateCall(
            int newId,
            string workItemType,
            IReadOnlyList<JsonObject> patchOperations,
            bool suppressNotifications)
        {
            NewId = newId;
            WorkItemType = workItemType;
            PatchOperations = patchOperations;
            SuppressNotifications = suppressNotifications;
        }

        public int NewId { get; }

        public string WorkItemType { get; }

        public IReadOnlyList<JsonObject> PatchOperations { get; }

        public bool SuppressNotifications { get; }
    }

    internal sealed class ParentLinkCall
    {
        public ParentLinkCall(int childId, int parentId, bool suppressNotifications)
        {
            ChildId = childId;
            ParentId = parentId;
            SuppressNotifications = suppressNotifications;
        }

        public int ChildId { get; }

        public int ParentId { get; }

        public bool SuppressNotifications { get; }
    }

    internal sealed class UploadAttachmentCall
    {
        public UploadAttachmentCall(string fileName, byte[] content, string url)
        {
            FileName = fileName;
            Content = content;
            Url = url;
        }

        public string FileName { get; }

        public byte[] Content { get; }

        public string Url { get; }
    }

    internal sealed class AttachmentRelationCall
    {
        public AttachmentRelationCall(int workItemId, string attachmentUrl, string? comment, bool suppressNotifications)
        {
            WorkItemId = workItemId;
            AttachmentUrl = attachmentUrl;
            Comment = comment;
            SuppressNotifications = suppressNotifications;
        }

        public int WorkItemId { get; }

        public string AttachmentUrl { get; }

        public string? Comment { get; }

        public bool SuppressNotifications { get; }
    }

    internal sealed class RelationCall
    {
        public RelationCall(
            int workItemId,
            string relationType,
            int? targetWorkItemId,
            string? targetUrl,
            string? comment,
            string? name,
            bool suppressNotifications)
        {
            WorkItemId = workItemId;
            RelationType = relationType;
            TargetWorkItemId = targetWorkItemId;
            TargetUrl = targetUrl;
            Comment = comment;
            Name = name;
            SuppressNotifications = suppressNotifications;
        }

        public int WorkItemId { get; }

        public string RelationType { get; }

        public int? TargetWorkItemId { get; }

        public string? TargetUrl { get; }

        public string? Comment { get; }

        public string? Name { get; }

        public bool SuppressNotifications { get; }
    }
}
