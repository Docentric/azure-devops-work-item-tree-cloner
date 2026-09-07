using System.Text.Json.Nodes;

using AdoWorkItemTreeCloner.Core.AzureDevOps;

namespace AdoWorkItemTreeCloner.Core.Tests.TestDoubles;

internal sealed class FakeAzureDevOpsClient : IAzureDevOpsClient
{
    private int _nextId = 1000;

    public Dictionary<int, JsonObject> WorkItems { get; } = [];

    public List<CreateCall> CreateCalls { get; } = [];

    public List<ParentLinkCall> ParentLinkCalls { get; } = [];

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
}
