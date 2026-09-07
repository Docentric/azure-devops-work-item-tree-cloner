using System.Net;
using System.Text;
using System.Text.Json.Nodes;

using AdoWorkItemTreeCloner.Core.AzureDevOps;

namespace AdoWorkItemTreeCloner.Core.Tests.AzureDevOps;

public sealed class AzureDevOpsClientTests
{
    [Fact]
    public async Task GetWorkItemAsync_UsesExpectedUriAndBasicAuthentication()
    {
        RecordingHttpMessageHandler handler = new RecordingHttpMessageHandler(
            _ => JsonResponse("{\"id\":123,\"fields\":{}}"));

        using AzureDevOpsClient client = CreateClient(handler);

        await client.GetWorkItemAsync(123, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal(
            "https://dev.azure.com/docentric/Project%20Name/_apis/wit/workitems/123?$expand=relations&api-version=7.1",
            handler.RequestUri);
        Assert.Equal("Basic", handler.AuthorizationScheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.ASCII.GetBytes(":secret")),
            handler.AuthorizationParameter);
    }

    [Fact]
    public async Task CreateWorkItemAsync_SendsJsonPatchAndSuppressNotifications()
    {
        RecordingHttpMessageHandler handler = new RecordingHttpMessageHandler(
            _ => JsonResponse("{\"id\":456,\"fields\":{}}"));

        using AzureDevOpsClient client = CreateClient(handler);
        JsonObject[] patch =
        [
            new JsonObject
            {
                ["op"] = "add",
                ["path"] = "/fields/System.Title",
                ["value"] = "Title"
            }
        ];

        int id = await client.CreateWorkItemAsync(
            "Product Backlog Item",
            patch,
            suppressNotifications: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(456, id);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Contains(
            "/_apis/wit/workitems/$Product%20Backlog%20Item?",
            handler.RequestUri,
            StringComparison.Ordinal);
        Assert.Contains("suppressNotifications=true", handler.RequestUri, StringComparison.Ordinal);
        Assert.Equal("application/json-patch+json", handler.ContentType);
        Assert.Contains("System.Title", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddParentRelationAsync_UsesHierarchyReverseRelation()
    {
        RecordingHttpMessageHandler handler = new RecordingHttpMessageHandler(_ => JsonResponse("{\"id\":456}"));
        using AzureDevOpsClient client = CreateClient(handler);

        await client.AddParentRelationAsync(
            childId: 456,
            parentId: 123,
            suppressNotifications: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Patch, handler.Method);
        Assert.Contains("System.LinkTypes.Hierarchy-Reverse", handler.Body, StringComparison.Ordinal);
        Assert.Contains(
            "https://dev.azure.com/docentric/_apis/wit/workItems/123",
            handler.Body,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Project%20Name/_apis/wit/workItems/123",
            handler.Body,
            StringComparison.Ordinal);
    }

    private static AzureDevOpsClient CreateClient(HttpMessageHandler handler) =>
        new(
            new AzureDevOpsClientOptions(
                "https://dev.azure.com/docentric",
                "Project Name",
                "secret"),
            handler);

    private static HttpResponseMessage JsonResponse(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string RequestUri { get; private set; } = string.Empty;

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        public string ContentType { get; private set; } = string.Empty;

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri?.AbsoluteUri ?? string.Empty;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            ContentType = request.Content?.Headers.ContentType?.MediaType ?? string.Empty;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responseFactory(request);
        }
    }
}
