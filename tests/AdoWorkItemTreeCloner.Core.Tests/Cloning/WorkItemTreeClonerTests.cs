using System.Text.Json.Nodes;

using AdoWorkItemTreeCloner.Core.AzureDevOps;
using AdoWorkItemTreeCloner.Core.Cloning;
using AdoWorkItemTreeCloner.Core.Tests.TestDoubles;

namespace AdoWorkItemTreeCloner.Core.Tests.Cloning;

public sealed class WorkItemTreeClonerTests
{
    private static readonly CloneOptions _defaultOptions = new(
        titleSuffix: " - Copy",
        copyAreaPath: true,
        copyIterationPath: false,
        copyAssignedTo: false,
        suppressNotifications: true);

    /// <summary>
    /// Verifies the full Parent/Child hierarchy is loaded recursively, preserving each node's children.
    /// </summary>
    [Fact]
    public async Task LoadTreeAsync_LoadsCompleteRecursiveHierarchy()
    {
        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = WorkItemJsonFactory.Create(1, "Epic", "Root", [2, 3]);
        client.WorkItems[2] = WorkItemJsonFactory.Create(2, "Feature", "Feature A", [4]);
        client.WorkItems[3] = WorkItemJsonFactory.Create(3, "Feature", "Feature B");
        client.WorkItems[4] = WorkItemJsonFactory.Create(4, "Product Backlog Item", "PBI");

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(4, tree.CountNodes());
        Assert.Equal("Root", tree.Title);
        Assert.Collection(
            tree.Children,
            featureA =>
            {
                Assert.Equal(2, featureA.Id);
#pragma warning disable xUnit2033 // Use the assertion return value instead of re-deriving it
                Assert.Single(featureA.Children);
#pragma warning restore xUnit2033 // Use the assertion return value instead of re-deriving it
                Assert.Equal(4, featureA.Children[0].Id);
            },
            featureB => Assert.Equal(3, featureB.Id));
    }

    /// <summary>
    /// Verifies non Parent/Child relations (such as Related) are ignored when building the hierarchy.
    /// </summary>
    [Fact]
    public async Task LoadTreeAsync_IgnoresNonChildRelations()
    {
        JsonObject root = WorkItemJsonFactory.Create(1, "Epic", "Root", [2]);
        root["relations"]!.AsArray().Add(
            new JsonObject
            {
                ["rel"] = "System.LinkTypes.Related",
                ["url"] = "https://dev.azure.com/example/_apis/wit/workItems/999"
            });

        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = root;
        client.WorkItems[2] = WorkItemJsonFactory.Create(2, "Feature", "Feature");

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);

        WorkItemNode child = Assert.Single(tree.Children);
        Assert.Equal(2, child.Id);
    }

    /// <summary>
    /// Verifies a cycle in the Parent/Child hierarchy is detected and reported instead of causing infinite recursion.
    /// </summary>
    [Fact]
    public async Task LoadTreeAsync_ThrowsWhenHierarchyContainsCycle()
    {
        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = WorkItemJsonFactory.Create(1, "Epic", "Root", [2]);
        client.WorkItems[2] = WorkItemJsonFactory.Create(2, "Feature", "Feature", [1]);

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);

        AzureDevOpsException exception = await Assert.ThrowsAsync<AzureDevOpsException>(
            () => cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken));

        Assert.Contains("Cycle detected", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies loading fails when a work item appears more than once in the hierarchy, since the source must be a tree.
    /// </summary>
    [Fact]
    public async Task LoadTreeAsync_ThrowsWhenItemOccursMoreThanOnce()
    {
        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = WorkItemJsonFactory.Create(1, "Epic", "Root", [2, 3]);
        client.WorkItems[2] = WorkItemJsonFactory.Create(2, "Feature", "Feature A", [4]);
        client.WorkItems[3] = WorkItemJsonFactory.Create(3, "Feature", "Feature B", [4]);
        client.WorkItems[4] = WorkItemJsonFactory.Create(4, "Task", "Shared child");

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);

        AzureDevOpsException exception = await Assert.ThrowsAsync<AzureDevOpsException>(
            () => cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken));

        Assert.Contains("occurs more than once", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies every node in the source tree is cloned and that Parent/Child links are recreated against the new IDs.
    /// </summary>
    [Fact]
    public async Task CloneAsync_ClonesEveryNodeAndRecreatesParentChildLinks()
    {
        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = WorkItemJsonFactory.Create(1, "Epic", "Root", [2]);
        client.WorkItems[2] = WorkItemJsonFactory.Create(2, "Feature", "Feature", [3]);
        client.WorkItems[3] = WorkItemJsonFactory.Create(3, "Task", "Task");

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);
        CloneResult result = await cloner.CloneAsync(tree, newParentId: null, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.IdMap.Count);
        Assert.Equal(1000, result.RootNewId);
        Assert.Equal(2, result.RelationCount);

        Assert.Collection(
            client.CreateCalls,
            rootCall =>
            {
                Assert.Equal("Epic", rootCall.WorkItemType);
                Assert.Equal("Root - Copy", GetFieldValue<string>(rootCall.PatchOperations, "System.Title"));
            },
            featureCall =>
            {
                Assert.Equal("Feature", featureCall.WorkItemType);
                Assert.Equal("Feature", GetFieldValue<string>(featureCall.PatchOperations, "System.Title"));
            },
            taskCall =>
            {
                Assert.Equal("Task", taskCall.WorkItemType);
                Assert.Equal("Task", GetFieldValue<string>(taskCall.PatchOperations, "System.Title"));
            });

        Assert.Collection(
            client.ParentLinkCalls,
            first =>
            {
                Assert.Equal(1001, first.ChildId);
                Assert.Equal(1000, first.ParentId);
                Assert.True(first.SuppressNotifications);
            },
            second =>
            {
                Assert.Equal(1002, second.ChildId);
                Assert.Equal(1001, second.ParentId);
                Assert.True(second.SuppressNotifications);
            });
    }

    /// <summary>
    /// Verifies that when a new parent ID is supplied, the cloned root is linked to that existing work item
    /// via a Parent/Child relation and the relation count reflects the extra link.
    /// </summary>
    [Fact]
    public async Task CloneAsync_LinksClonedRootToNewParentWhenProvided()
    {
        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = WorkItemJsonFactory.Create(1, "Epic", "Root");

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);
        CloneResult result = await cloner.CloneAsync(tree, newParentId: 42, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.RelationCount);
        FakeAzureDevOpsClient.ParentLinkCall link = Assert.Single(client.ParentLinkCalls);
        Assert.Equal(result.RootNewId, link.ChildId);
        Assert.Equal(42, link.ParentId);
        Assert.True(link.SuppressNotifications);
    }

    private static T GetFieldValue<T>(
        IEnumerable<JsonObject> operations,
        string fieldName)
    {
        JsonObject operation = operations.Single(
            item => string.Equals(
                item["path"]?.GetValue<string>(),
                $"/fields/{fieldName}",
                StringComparison.Ordinal));

        return operation["value"]!.GetValue<T>();
    }
}
