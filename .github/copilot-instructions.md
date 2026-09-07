# Copilot Instructions — AdoWorkItemTreeCloner

## Tech stack

- **.NET 10**, **C# 14**, `Nullable` and `ImplicitUsings` enabled.
- Solution layout:
  - `src/AdoWorkItemTreeCloner.Core` — core library (Azure DevOps client, cloning logic).
  - `src/AdoWorkItemTreeCloner` — CLI application (`Cli/`, `Program.cs`).
  - `tests/AdoWorkItemTreeCloner.Core.Tests` — xUnit v3 test project.
- Testing: **xUnit** (`[Fact]` / `[Theory]`), no test class attribute required. Use `TestContext.Current.CancellationToken` instead of `CancellationToken.None`.
- Analyzers: `NewStyleCop.Analyzers` + .NET analyzers (`AnalysisLevel=latest-recommended`). Warnings are treated as errors (`TreatWarningsAsErrors=true`) — code must build with zero warnings.
- HTTP/JSON: `System.Text.Json` (`JsonObject`/`JsonArray`/`JsonNode`), `HttpClient` with injectable `HttpMessageHandler` for testability.

## Code style (enforced by `.editorconfig` and `stylecop.json`)

- File-scoped namespaces only (`namespace Foo.Bar;`), enforced as an error.
- 4-space indentation for C# files; braces always required (`csharp_prefer_braces = true:error`).
- Private fields: `_camelCase`. Private const fields: `PascalCase`. Interfaces: prefixed with `I`.
- Qualify members without `this.`; do not add unnecessary member qualification.
- Prefer `var` only when the type is apparent or built-in; use explicit types elsewhere.
- Prefer pattern matching, switch expressions, object/collection initializers, null-coalescing and null-propagation where they read cleanly.
- `using` directives outside the namespace, `System` usings sorted first, groups separated by a blank line.
- Guard clauses: `ArgumentNullException.ThrowIfNull(x)` for reference types; validate required strings explicitly (see `ValidateRequiredValue` pattern in `AzureDevOpsClient`).
- Async methods end with `Async`, accept and forward a `CancellationToken`, and use `.ConfigureAwait(false)` in library code (`Core` project). CLI entry-point code may omit it.
- Do not edit generated code or files under `obj/`/`bin/`.
- Company name for any generated headers is `Docentric` (see `stylecop.json`), but per-project settings disable mandatory doc comments for StyleCop (`SA1600`/`SA1633`) — documentation is still required by these instructions for public API and tests (see below), just not auto-enforced by the analyzer.

## XML documentation

Documentation must be **short, understandable, and describe what the member does and, when not obvious, why it exists**. Avoid restating the signature; avoid filler text.

Required on:
- All public types (classes, records, interfaces, enums) — one or two sentence `<summary>`.
- All public methods and constructors — `<summary>`, plus `<param>` for non-obvious parameters and `<returns>` when the return value isn't self-explanatory.
- Interface implementations should use `/// <inheritdoc />` rather than repeating the summary, unless the implementation has behavior worth calling out.
- All test methods (see below) — a one-line `<summary>` stating what is being verified and, if useful, why.

Not required on private/internal members unless the logic is non-obvious.

Example:

```csharp
/// <summary>
/// Minimal Azure DevOps Work Item Tracking REST client.
/// </summary>
public sealed class AzureDevOpsClient : IAzureDevOpsClient, IDisposable
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AzureDevOpsClient"/> class.
	/// </summary>
	/// <param name="options">Client configuration.</param>
	/// <param name="handler">Optional HTTP handler, primarily for testing.</param>
	public AzureDevOpsClient(AzureDevOpsClientOptions options, HttpMessageHandler? handler = null)
```

## Tests

- Write tests using **xUnit**, following the Arrange-Act-Assert pattern with one behavior asserted per test.
- Name test methods `MethodUnderTest_ExpectedBehavior` (e.g., `GetWorkItemAsync_UsesExpectedUriAndBasicAuthentication`), matching the style already used in `AzureDevOpsClientTests`.
- Every test method must have a one-line `<summary>` doc comment describing **what** is verified and **why** it matters, e.g.:

  ```csharp
  /// <summary>
  /// Verifies the client requests the work item with expanded relations and Basic authentication,
  /// since Azure DevOps rejects requests without a valid auth header.
  /// </summary>
  [Fact]
  public async Task GetWorkItemAsync_UsesExpectedUriAndBasicAuthentication()
  ```

- Mirror the class under test with a `<ClassName>Tests` class in the matching folder (e.g., `AzureDevOps/AzureDevOpsClient.cs` -> `AzureDevOps/AzureDevOpsClientTests.cs`).
- Prefer test doubles already present in `TestDoubles/` (e.g., `FakeAzureDevOpsClient`, `WorkItemJsonFactory`, `RecordingHttpMessageHandler`) over introducing new mocking libraries. Only fake external dependencies (HTTP, Azure DevOps API) — never fake code owned by this solution.
- Use `TestContext.Current.CancellationToken` when a `CancellationToken` is required.
- Assert specific values (status codes, URIs, JSON payload fragments) rather than vague truthy checks.
- Keep tests independent and order-agnostic; avoid shared mutable state between tests.

## Commit messages

Keep commits short and scannable:

- **Header**: a single concise line (imperative mood, e.g., `Add retry policy to AzureDevOpsClient`), ideally under ~72 characters, no trailing period.
- **Body**: a bullet list below the header explaining **what** changed and **why**, e.g.:

  ```
  Add retry policy to AzureDevOpsClient

  - Retry transient HTTP failures up to 4 times with exponential backoff, because
	Azure DevOps occasionally throttles requests during bulk cloning.
  - Extract MaximumAttempts constant to make the retry count configurable in one place.
  ```

- Do not combine unrelated changes into a single commit.
