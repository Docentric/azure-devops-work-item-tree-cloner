using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AdoWorkItemTreeCloner.Core.AzureDevOps;

/// <summary>
/// Minimal Azure DevOps Work Item Tracking REST client.
/// </summary>
public sealed class AzureDevOpsClient : IAzureDevOpsClient, IDisposable
{
    private const string ApiVersion = "7.1";
    private const int MaximumAttempts = 4;

    private readonly HttpClient _httpClient;
    private readonly Uri _organizationBaseUri;
    private readonly Uri _projectBaseUri;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDevOpsClient"/> class.
    /// </summary>
    /// <param name="options">Client configuration.</param>
    /// <param name="handler">Optional HTTP handler, primarily for testing.</param>
    public AzureDevOpsClient(AzureDevOpsClientOptions options, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateRequiredValue(options.Organization, nameof(options.Organization));
        ValidateRequiredValue(options.Project, nameof(options.Project));
        ValidateRequiredValue(options.PersonalAccessToken, nameof(options.PersonalAccessToken));

        var organization = options.Organization.TrimEnd('/');
        if (!Uri.TryCreate($"{organization}/", UriKind.Absolute, out Uri? organizationBaseUri))
        {
            throw new ArgumentException("Organization must be an absolute URI.", nameof(options));
        }

        _organizationBaseUri = organizationBaseUri;

        _projectBaseUri = new Uri(
            _organizationBaseUri,
            $"{Uri.EscapeDataString(options.Project)}/");

        _httpClient = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: true);

        var basicToken = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($":{options.PersonalAccessToken}"));

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", basicToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("AdoWorkItemTreeCloner", "1.0"));
    }

    /// <inheritdoc />
    public Task<JsonObject> GetWorkItemAsync(int id, CancellationToken cancellationToken)
    {
        ValidateWorkItemId(id, nameof(id));

        var relativeUri = $"_apis/wit/workitems/{id}?$expand=relations&api-version={ApiVersion}";
        return SendForJsonAsync(
            HttpMethod.Get,
            new Uri(_projectBaseUri, relativeUri),
            body: null,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CreateWorkItemAsync(
        string workItemType,
        IReadOnlyList<JsonObject> patchOperations,
        bool suppressNotifications,
        CancellationToken cancellationToken)
    {
        ValidateRequiredValue(workItemType, nameof(workItemType));
        ArgumentNullException.ThrowIfNull(patchOperations);

        var encodedType = Uri.EscapeDataString(workItemType);
        var notificationValue = suppressNotifications ? "true" : "false";
        var relativeUri =
            $"_apis/wit/workitems/${encodedType}?suppressNotifications={notificationValue}&api-version={ApiVersion}";
        JsonArray body = [.. patchOperations.Select(static operation => operation.DeepClone()).ToArray()];

        JsonObject response = await SendForJsonAsync(
                HttpMethod.Post,
                new Uri(_projectBaseUri, relativeUri),
                body,
                cancellationToken)
            .ConfigureAwait(false);

        return response["id"]?.GetValue<int>()
            ?? throw new AzureDevOpsException(
                "Azure DevOps did not return an ID for the newly created work item.");
    }

    /// <inheritdoc />
    public async Task AddParentRelationAsync(
        int childId,
        int parentId,
        bool suppressNotifications,
        CancellationToken cancellationToken)
    {
        ValidateWorkItemId(childId, nameof(childId));
        ValidateWorkItemId(parentId, nameof(parentId));

        // Work-item relation URLs are organization-scoped even when the update API call
        // itself is project-scoped.
        var parentUrl = new Uri(
            _organizationBaseUri,
            $"_apis/wit/workItems/{parentId}").AbsoluteUri;

        JsonArray patch =
        [
            new JsonObject
            {
                ["op"] = "add",
                ["path"] = "/relations/-",
                ["value"] = new JsonObject
                {
                    ["rel"] = "System.LinkTypes.Hierarchy-Reverse",
                    ["url"] = parentUrl,
                    ["attributes"] = new JsonObject
                    {
                        ["comment"] = "Created by AdoWorkItemTreeCloner"
                    }
                }
            }
        ];

        var notificationValue = suppressNotifications ? "true" : "false";
        var relativeUri =
            $"_apis/wit/workitems/{childId}?suppressNotifications={notificationValue}&api-version={ApiVersion}";

        _ = await SendForJsonAsync(
                HttpMethod.Patch,
                new Uri(_projectBaseUri, relativeUri),
                patch,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddRelationAsync(
        int workItemId,
        string relationType,
        int? targetWorkItemId,
        string? targetUrl,
        string? comment,
        string? name,
        bool suppressNotifications,
        CancellationToken cancellationToken)
    {
        ValidateWorkItemId(workItemId, nameof(workItemId));
        ValidateRequiredValue(relationType, nameof(relationType));

        string resolvedUrl;
        if (targetWorkItemId.HasValue)
        {
            ValidateWorkItemId(targetWorkItemId.Value, nameof(targetWorkItemId));

            // Work-item relation URLs are organization-scoped even when the update API call
            // itself is project-scoped.
            resolvedUrl = new Uri(
                _organizationBaseUri,
                $"_apis/wit/workItems/{targetWorkItemId.Value}").AbsoluteUri;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                throw new ArgumentException(
                    "Either targetWorkItemId or targetUrl must be provided.",
                    nameof(targetUrl));
            }

            resolvedUrl = targetUrl;
        }

        JsonObject relation = new()
        {
            ["rel"] = relationType,
            ["url"] = resolvedUrl
        };

        if (!string.IsNullOrWhiteSpace(comment) || !string.IsNullOrWhiteSpace(name))
        {
            JsonObject attributes = [];

            if (!string.IsNullOrWhiteSpace(name))
            {
                attributes["name"] = name;
            }

            if (!string.IsNullOrWhiteSpace(comment))
            {
                attributes["comment"] = comment;
            }

            relation["attributes"] = attributes;
        }

        JsonArray patch =
        [
            new JsonObject
            {
                ["op"] = "add",
                ["path"] = "/relations/-",
                ["value"] = relation
            }
        ];

        var notificationValue = suppressNotifications ? "true" : "false";
        var relativeUri =
            $"_apis/wit/workitems/{workItemId}?suppressNotifications={notificationValue}&api-version={ApiVersion}";

        _ = await SendForJsonAsync(
                HttpMethod.Patch,
                new Uri(_projectBaseUri, relativeUri),
                patch,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<byte[]> DownloadAttachmentAsync(Uri attachmentUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachmentUrl);

        using HttpResponseMessage response = await _httpClient.GetAsync(attachmentUrl, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            var errorMessage = TryGetErrorMessage(errorContent) ?? errorContent;
            throw new AzureDevOpsException(
                $"{(int)response.StatusCode} {response.ReasonPhrase}: {errorMessage}".Trim());
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> UploadAttachmentAsync(
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        ValidateRequiredValue(fileName, nameof(fileName));
        ArgumentNullException.ThrowIfNull(content);

        var relativeUri =
            $"_apis/wit/attachments?fileName={Uri.EscapeDataString(fileName)}&api-version={ApiVersion}";

        using var byteContent = new ByteArrayContent(content);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_projectBaseUri, relativeUri))
        {
            Content = byteContent
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = TryGetErrorMessage(responseContent) ?? responseContent;
            throw new AzureDevOpsException(
                $"{(int)response.StatusCode} {response.ReasonPhrase}: {errorMessage}".Trim());
        }

        JsonObject json = JsonNode.Parse(responseContent)?.AsObject()
            ?? throw new AzureDevOpsException("Azure DevOps returned an invalid JSON response.");

        return json["url"]?.GetValue<string>()
            ?? throw new AzureDevOpsException(
                "Azure DevOps did not return a URL for the uploaded attachment.");
    }

    /// <inheritdoc />
    public async Task AddAttachmentRelationAsync(
        int workItemId,
        string attachmentUrl,
        string? comment,
        bool suppressNotifications,
        CancellationToken cancellationToken)
    {
        ValidateWorkItemId(workItemId, nameof(workItemId));
        ValidateRequiredValue(attachmentUrl, nameof(attachmentUrl));

        JsonObject relation = new()
        {
            ["rel"] = "AttachedFile",
            ["url"] = attachmentUrl
        };

        if (!string.IsNullOrWhiteSpace(comment))
        {
            relation["attributes"] = new JsonObject
            {
                ["comment"] = comment
            };
        }

        JsonArray patch =
        [
            new JsonObject
            {
                ["op"] = "add",
                ["path"] = "/relations/-",
                ["value"] = relation
            }
        ];

        var notificationValue = suppressNotifications ? "true" : "false";
        var relativeUri =
            $"_apis/wit/workitems/{workItemId}?suppressNotifications={notificationValue}&api-version={ApiVersion}";

        _ = await SendForJsonAsync(
                HttpMethod.Patch,
                new Uri(_projectBaseUri, relativeUri),
                patch,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteWorkItemAsync(int id, CancellationToken cancellationToken)
    {
        ValidateWorkItemId(id, nameof(id));

        var relativeUri = $"_apis/wit/workitems/{id}?api-version={ApiVersion}";

        _ = await SendForJsonAsync(
                HttpMethod.Delete,
                new Uri(_projectBaseUri, relativeUri),
                body: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } retryDate)
        {
            TimeSpan delay = retryDate - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
    }

    private static string? TryGetErrorMessage(string content)
    {
        try
        {
            return JsonNode.Parse(content)?["message"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void ValidateRequiredValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null, empty, or whitespace.", parameterName);
        }
    }

    private static void ValidateWorkItemId(int id, string parameterName)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                id,
                "Work item ID must be greater than zero.");
        }
    }

    private async Task<JsonObject> SendForJsonAsync(
        HttpMethod method,
        Uri uri,
        JsonNode? body,
        CancellationToken cancellationToken)
    {
        var serializedBody = body?.ToJsonString();

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(method, uri);
            if (serializedBody is not null)
            {
                request.Content = new StringContent(
                    serializedBody,
                    Encoding.UTF8,
                    "application/json-patch+json");
            }

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            var content = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    return [];
                }

                return JsonNode.Parse(content)?.AsObject()
                    ?? throw new AzureDevOpsException(
                        "Azure DevOps returned an invalid JSON response.");
            }

            if (attempt < MaximumAttempts && IsTransient(response.StatusCode))
            {
                await Task.Delay(GetRetryDelay(response, attempt), cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            var message = TryGetErrorMessage(content) ?? content;
            throw new AzureDevOpsException(
                $"{(int)response.StatusCode} {response.ReasonPhrase}: {message}".Trim());
        }

        throw new AzureDevOpsException("Azure DevOps request failed after all retry attempts.");
    }
}
