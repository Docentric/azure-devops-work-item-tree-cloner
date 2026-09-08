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
        copyAttachments: false,
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
    /// Verifies children are ordered by their Stack Rank field rather than the order links happen to appear
    /// in the relations array, since the relations array reflects link-creation order, not backlog order.
    /// </summary>
    [Fact]
    public async Task LoadTreeAsync_OrdersChildrenByStackRankInsteadOfRelationOrder()
    {
        var client = new FakeAzureDevOpsClient();

        // Relations array lists 4 before 2 and 3, but Stack Rank says the backlog order is 2, 3, 4.
        client.WorkItems[1] = WorkItemJsonFactory.Create(1, "Epic", "Root", [4, 2, 3]);
        client.WorkItems[2] = WorkItemJsonFactory.Create(
            2, "Feature", "Feature A", configureFields: fields => fields["Microsoft.VSTS.Common.StackRank"] = 100.0);
        client.WorkItems[3] = WorkItemJsonFactory.Create(
            3, "Feature", "Feature B", configureFields: fields => fields["Microsoft.VSTS.Common.StackRank"] = 200.0);
        client.WorkItems[4] = WorkItemJsonFactory.Create(
            4, "Feature", "Feature C", configureFields: fields => fields["Microsoft.VSTS.Common.StackRank"] = 300.0);

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);

        Assert.Collection(
            tree.Children,
            child => Assert.Equal(2, child.Id),
            child => Assert.Equal(3, child.Id),
            child => Assert.Equal(4, child.Id));
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
    /// Verifies AttachedFile relations are captured as attachments on the loaded node, since the cloner
    /// needs the source URL and comment to recreate them on the clone.
    /// </summary>
    [Fact]
    public async Task LoadTreeAsync_CapturesAttachmentRelations()
    {
        JsonObject root = WorkItemJsonFactory.Create(1, "Epic", "Root");
        root["relations"]!.AsArray().Add(
            new JsonObject
            {
                ["rel"] = "AttachedFile",
                ["url"] = "https://dev.azure.com/example/_apis/wit/attachments/abc?fileName=notes.txt",
                ["attributes"] = new JsonObject { ["name"] = "notes.txt", ["comment"] = "Design notes" }
            });

        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = root;

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);

        WorkItemAttachment attachment = Assert.Single(tree.Attachments);
        Assert.Equal(
            "https://dev.azure.com/example/_apis/wit/attachments/abc?fileName=notes.txt",
            attachment.Url);
        Assert.Equal("notes.txt", attachment.FileName);
        Assert.Equal("Design notes", attachment.Comment);
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

    /// <summary>
    /// Verifies attachments are downloaded from the source, re-uploaded, and linked to the cloned work item
    /// when attachment copying is enabled.
    /// </summary>
    [Fact]
    public async Task CloneAsync_CopiesAttachmentsWhenEnabled()
    {
        const string attachmentUrl = "https://dev.azure.com/example/_apis/wit/attachments/abc?fileName=notes.txt";
        byte[] attachmentContent = [1, 2, 3];

        JsonObject root = WorkItemJsonFactory.Create(1, "Epic", "Root");
        root["relations"]!.AsArray().Add(
            new JsonObject
            {
                ["rel"] = "AttachedFile",
                ["url"] = attachmentUrl,
                ["attributes"] = new JsonObject { ["name"] = "notes.txt", ["comment"] = "Design notes" }
            });

        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = root;
        client.AttachmentContents[attachmentUrl] = attachmentContent;

        var options = new CloneOptions(
            titleSuffix: " - Copy",
            copyAreaPath: true,
            copyIterationPath: false,
            copyAssignedTo: false,
            copyAttachments: true,
            suppressNotifications: true);

        var cloner = new WorkItemTreeCloner(client, options);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);
        CloneResult result = await cloner.CloneAsync(tree, newParentId: null, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.AttachmentCount);
        FakeAzureDevOpsClient.UploadAttachmentCall upload = Assert.Single(client.UploadAttachmentCalls);
        Assert.Equal("notes.txt", upload.FileName);
        Assert.Equal(attachmentContent, upload.Content);

        FakeAzureDevOpsClient.AttachmentRelationCall relation = Assert.Single(client.AttachmentRelationCalls);
        Assert.Equal(result.RootNewId, relation.WorkItemId);
        Assert.Equal(upload.Url, relation.AttachmentUrl);
        Assert.Equal("Design notes", relation.Comment);
    }

    /// <summary>
    /// Verifies attachments are neither downloaded nor uploaded when attachment copying is disabled.
    /// </summary>
    [Fact]
    public async Task CloneAsync_SkipsAttachmentsWhenDisabled()
    {
        const string attachmentUrl = "https://dev.azure.com/example/_apis/wit/attachments/abc?fileName=notes.txt";

        JsonObject root = WorkItemJsonFactory.Create(1, "Epic", "Root");
        root["relations"]!.AsArray().Add(
            new JsonObject
            {
                ["rel"] = "AttachedFile",
                ["url"] = attachmentUrl
            });

        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = root;
        client.AttachmentContents[attachmentUrl] = [1, 2, 3];

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);
        CloneResult result = await cloner.CloneAsync(tree, newParentId: null, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.AttachmentCount);
        Assert.Empty(client.UploadAttachmentCalls);
        Assert.Empty(client.AttachmentRelationCalls);
    }

    /// <summary>
    /// Verifies non-hierarchy relations (such as Related) are captured on the loaded node with their
    /// relation type, parsed target ID, original URL, and comment, since they must be recreated after cloning.
    /// </summary>
    [Fact]
    public async Task LoadTreeAsync_CapturesOtherRelations()
    {
        JsonObject root = WorkItemJsonFactory.Create(1, "Epic", "Root");
        root["relations"]!.AsArray().Add(
            new JsonObject
            {
                ["rel"] = "System.LinkTypes.Related",
                ["url"] = "https://dev.azure.com/example/Project/_apis/wit/workItems/999",
                ["attributes"] = new JsonObject { ["comment"] = "See also" }
            });

        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = root;

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);

        WorkItemRelation relation = Assert.Single(tree.OtherRelations);
        Assert.Equal("System.LinkTypes.Related", relation.RelationType);
        Assert.Equal(999, relation.TargetId);
        Assert.Equal("https://dev.azure.com/example/Project/_apis/wit/workItems/999", relation.Url);
        Assert.Equal("See also", relation.Comment);
    }

    /// <summary>
    /// Verifies the link back to a node's own parent (Hierarchy-Reverse) is not captured as an "other" relation,
    /// since it is already recreated separately via the Parent/Child cloning logic.
    /// </summary>
    [Fact]
    public async Task LoadTreeAsync_IgnoresOwnHierarchyReverseRelation()
    {
        JsonObject root = WorkItemJsonFactory.Create(1, "Epic", "Root", [2]);
        JsonObject child = WorkItemJsonFactory.Create(2, "Feature", "Feature");
        child["relations"]!.AsArray().Add(
            new JsonObject
            {
                ["rel"] = "System.LinkTypes.Hierarchy-Reverse",
                ["url"] = "https://dev.azure.com/example/Project/_apis/wit/workItems/1"
            });

        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = root;
        client.WorkItems[2] = child;

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);

        Assert.Empty(tree.Children[0].OtherRelations);
    }

    /// <summary>
    /// Verifies a relation whose target work item is not part of the cloned tree is recreated on the clone
    /// pointing at the original, un-cloned target rather than being remapped.
    /// </summary>
    [Fact]
    public async Task CloneAsync_PreservesRelationToExternalWorkItem()
    {
        const string externalUrl = "https://dev.azure.com/example/Project/_apis/wit/workItems/999";

        JsonObject root = WorkItemJsonFactory.Create(1, "Epic", "Root");
        root["relations"]!.AsArray().Add(
            new JsonObject
            {
                ["rel"] = "System.LinkTypes.Related",
                ["url"] = externalUrl
            });

        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = root;

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);
        CloneResult result = await cloner.CloneAsync(tree, newParentId: null, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.PreservedRelationCount);
        FakeAzureDevOpsClient.RelationCall call = Assert.Single(client.RelationCalls);
        Assert.Equal(result.RootNewId, call.WorkItemId);
        Assert.Equal("System.LinkTypes.Related", call.RelationType);
        Assert.Null(call.TargetWorkItemId);
        Assert.Equal(externalUrl, call.TargetUrl);
    }

    /// <summary>
    /// Verifies a relation whose target work item is itself part of the cloned tree is recreated on the clone
    /// pointing at the newly cloned target, since the original target ID is no longer valid in the new tree.
    /// </summary>
    [Fact]
    public async Task CloneAsync_RemapsRelationToClonedWorkItem()
    {
        JsonObject root = WorkItemJsonFactory.Create(1, "Epic", "Root", [2]);
        JsonObject child = WorkItemJsonFactory.Create(2, "Feature", "Feature");
        child["relations"]!.AsArray().Add(
            new JsonObject
            {
                ["rel"] = "System.LinkTypes.Related",
                ["url"] = "https://dev.azure.com/example/Project/_apis/wit/workItems/1"
            });

        var client = new FakeAzureDevOpsClient();
        client.WorkItems[1] = root;
        client.WorkItems[2] = child;

        var cloner = new WorkItemTreeCloner(client, _defaultOptions);
        WorkItemNode tree = await cloner.LoadTreeAsync(1, TestContext.Current.CancellationToken);
        CloneResult result = await cloner.CloneAsync(tree, newParentId: null, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.PreservedRelationCount);
        FakeAzureDevOpsClient.RelationCall call = Assert.Single(client.RelationCalls);
        Assert.Equal(result.IdMap[2], call.WorkItemId);
        Assert.Equal("System.LinkTypes.Related", call.RelationType);
        Assert.Equal(result.RootNewId, call.TargetWorkItemId);
        Assert.Null(call.TargetUrl);
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
