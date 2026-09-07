using System.Text.Json.Nodes;

namespace AdoWorkItemTreeCloner.Core.Tests.TestDoubles;

internal static class WorkItemJsonFactory
{
    public static JsonObject Create(
        int id,
        string type,
        string title,
        IEnumerable<int>? children = null,
        Action<JsonObject>? configureFields = null)
    {
        var fields = new JsonObject
        {
            ["System.WorkItemType"] = type,
            ["System.Title"] = title,
            ["System.AreaPath"] = "Project\\Area",
            ["System.IterationPath"] = "Project\\Sprint 1",
            ["System.State"] = "Active",
            ["System.CreatedDate"] = "2026-09-01T10:00:00Z",
            ["Microsoft.VSTS.Common.Priority"] = 2
        };

        configureFields?.Invoke(fields);

        JsonArray relations = [];
        foreach (var childId in children ?? [])
        {
            relations.Add(
                new JsonObject
                {
                    ["rel"] = "System.LinkTypes.Hierarchy-Forward",
                    ["url"] = $"https://dev.azure.com/example/Project/_apis/wit/workItems/{childId}"
                });
        }

        return new JsonObject
        {
            ["id"] = id,
            ["fields"] = fields,
            ["relations"] = relations
        };
    }
}
