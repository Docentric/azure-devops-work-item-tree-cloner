using System.Text.Json.Nodes;

using AdoWorkItemTreeCloner.Core.Cloning;

namespace AdoWorkItemTreeCloner.Core.Tests.Cloning;

public sealed class WorkItemCreatePatchBuilderTests
{
    /// <summary>
    /// Verifies useful and custom fields are copied while server-managed fields (state, dates) are excluded.
    /// </summary>
    [Fact]
    public void Build_CopiesUsefulAndCustomFieldsButNotServerManagedFields()
    {
        WorkItemNode node = CreateNode(
            fields =>
            {
                fields["System.Description"] = "Description";
                fields["System.State"] = "Active";
                fields["System.CreatedDate"] = "2026-09-01T10:00:00Z";
                fields["Microsoft.VSTS.Common.Priority"] = 1;
                fields["Custom.Release"] = "3.6.0";
            });

        IReadOnlyList<JsonObject> operations = WorkItemCreatePatchBuilder.Build(node, isRoot: true, DefaultOptions());

        Assert.Equal("Root - Copy", GetFieldValue<string>(operations, "System.Title"));
        Assert.Equal("Description", GetFieldValue<string>(operations, "System.Description"));
        Assert.Equal(1, GetFieldValue<int>(operations, "Microsoft.VSTS.Common.Priority"));
        Assert.Equal("3.6.0", GetFieldValue<string>(operations, "Custom.Release"));
        Assert.DoesNotContain(operations, operation => HasField(operation, "System.State"));
        Assert.DoesNotContain(operations, operation => HasField(operation, "System.CreatedDate"));
    }

    /// <summary>
    /// Verifies area path, iteration path, and assigned-to are copied only when the corresponding clone option is enabled.
    /// </summary>
    [Fact]
    public void Build_RespectsClassificationAndAssignmentOptions()
    {
        WorkItemNode node = CreateNode(
            fields =>
            {
                fields["System.AreaPath"] = "Project\\Area";
                fields["System.IterationPath"] = "Project\\Sprint 1";
                fields["System.AssignedTo"] = new JsonObject
                {
                    ["displayName"] = "User Name",
                    ["uniqueName"] = "user@example.com"
                };
            });

        var options = new CloneOptions(
            titleSuffix: " - Clone",
            copyAreaPath: false,
            copyIterationPath: true,
            copyAssignedTo: true,
            suppressNotifications: true);

        IReadOnlyList<JsonObject> operations = WorkItemCreatePatchBuilder.Build(node, isRoot: false, options);

        Assert.DoesNotContain(operations, operation => HasField(operation, "System.AreaPath"));
        Assert.Equal("Project\\Sprint 1", GetFieldValue<string>(operations, "System.IterationPath"));
        Assert.Equal("user@example.com", GetFieldValue<string>(operations, "System.AssignedTo"));
        Assert.Equal("Root", GetFieldValue<string>(operations, "System.Title"));
    }

    /// <summary>
    /// Verifies backlog ordering fields are always excluded, since they are re-assigned by Azure DevOps on create.
    /// </summary>
    [Fact]
    public void Build_ExcludesBacklogOrderingFields()
    {
        WorkItemNode node = CreateNode(
            fields =>
            {
                fields["Microsoft.VSTS.Common.StackRank"] = 123.4;
                fields["Microsoft.VSTS.Common.BacklogPriority"] = 456.7;
            });

        IReadOnlyList<JsonObject> operations = WorkItemCreatePatchBuilder.Build(node, isRoot: false, DefaultOptions());

        Assert.DoesNotContain(operations, operation => HasField(operation, "Microsoft.VSTS.Common.StackRank"));
        Assert.DoesNotContain(operations, operation => HasField(operation, "Microsoft.VSTS.Common.BacklogPriority"));
    }

    private static WorkItemNode CreateNode(Action<JsonObject> configureFields)
    {
        var fields = new JsonObject
        {
            ["System.Title"] = "Root",
            ["System.WorkItemType"] = "Epic"
        };

        configureFields(fields);

        return new WorkItemNode
        {
            Id = 1,
            WorkItemType = "Epic",
            Title = "Root",
            Fields = fields
        };
    }

    private static CloneOptions DefaultOptions() =>
        new(
            titleSuffix: " - Copy",
            copyAreaPath: true,
            copyIterationPath: false,
            copyAssignedTo: false,
            suppressNotifications: true);

    private static T GetFieldValue<T>(
        IEnumerable<JsonObject> operations,
        string fieldName)
    {
        JsonObject operation = operations.Single(item => HasField(item, fieldName));
        return operation["value"]!.GetValue<T>();
    }

    private static bool HasField(JsonObject operation, string fieldName) =>
        string.Equals(
            operation["path"]?.GetValue<string>(),
            $"/fields/{fieldName}",
            StringComparison.Ordinal);
}
