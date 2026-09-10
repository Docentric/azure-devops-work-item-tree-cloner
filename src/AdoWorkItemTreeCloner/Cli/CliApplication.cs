using System.CommandLine;
using System.Reflection;

using AdoWorkItemTreeCloner.Core.AzureDevOps;
using AdoWorkItemTreeCloner.Core.Cloning;

using Spectre.Console;

namespace AdoWorkItemTreeCloner.Cli;

internal static class CliApplication
{
    public static Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        ConsoleRenderer.RenderBanner();
        return CreateRootCommand().Parse(args).InvokeAsync();
    }

    private static RootCommand CreateRootCommand()
    {
        Option<string?> organizationOption = new("--organization")
        {
            Description = "Azure DevOps organization URL, for example https://dev.azure.com/docentric."
        };

        Option<string?> projectOption = new("--project")
        {
            Description = "Azure DevOps project containing the source tree and receiving the clone."
        };

        Option<int> rootOption = new("--root")
        {
            Description = "ID of the root work item to clone."
        };

        Option<string?> patOption = new("--pat")
        {
            Description = "Azure DevOps PAT. Prefer the AZURE_DEVOPS_PAT environment variable."
        };

        Option<string> titleSuffixOption = new("--title-suffix")
        {
            Description = "Suffix appended only to the cloned root work item title.",
            DefaultValueFactory = _ => " - Copy"
        };

        Option<bool> dryRunOption = new("--dry-run")
        {
            Description = "Read and display the complete tree without creating work items."
        };

        Option<bool> resetAreaPathOption = new("--reset-area-path")
        {
            Description = "Do not copy System.AreaPath. By default, Area Path is copied."
        };

        Option<bool> copyIterationPathOption = new("--copy-iteration-path")
        {
            Description = "Copy System.IterationPath. By default, the target process default is used."
        };

        Option<bool> copyAssignedToOption = new("--copy-assigned-to")
        {
            Description = "Copy System.AssignedTo. By default, Assigned To is left empty/defaulted."
        };

        Option<bool> resetAttachmentsOption = new("--no-copy-attachments")
        {
            Description = "Do not copy attachments. By default, attachments are copied."
        };

        Option<bool> notifyOption = new("--notify")
        {
            Description = "Allow Azure DevOps notifications for created/updated work items. Notifications are suppressed by default."
        };

        Option<int?> newParentIdOption = new("--newparentid")
        {
            Description = "ID of an existing Azure DevOps work item that becomes the parent of the newly created root work item. " +
                "When omitted, the cloned root is created without a parent."
        };

        RootCommand rootCommand = new(GetAssemblyDescription())
        {
            organizationOption,
            projectOption,
            rootOption,
            patOption,
            titleSuffixOption,
            dryRunOption,
            resetAreaPathOption,
            copyIterationPathOption,
            copyAssignedToOption,
            resetAttachmentsOption,
            notifyOption,
            newParentIdOption
        };

        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            CommandLineOptions? options = BindOptions(
                parseResult,
                organizationOption,
                projectOption,
                rootOption,
                patOption,
                titleSuffixOption,
                dryRunOption,
                resetAreaPathOption,
                copyIterationPathOption,
                copyAssignedToOption,
                resetAttachmentsOption,
                notifyOption,
                newParentIdOption);

            if (options is null)
            {
                return 2;
            }

            return await ExecuteAsync(options, cancellationToken);
        });

        return rootCommand;
    }

    private static string GetAssemblyDescription() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ??
        "Recursively clone an Azure DevOps Parent/Child work item tree.";

    private static CommandLineOptions? BindOptions(
        ParseResult parseResult,
        Option<string?> organizationOption,
        Option<string?> projectOption,
        Option<int> rootOption,
        Option<string?> patOption,
        Option<string> titleSuffixOption,
        Option<bool> dryRunOption,
        Option<bool> resetAreaPathOption,
        Option<bool> copyIterationPathOption,
        Option<bool> copyAssignedToOption,
        Option<bool> resetAttachmentsOption,
        Option<bool> notifyOption,
        Option<int?> newParentIdOption)
    {
        var organization = parseResult.GetValue(organizationOption);
        var project = parseResult.GetValue(projectOption);
        var rootId = parseResult.GetValue(rootOption);
        var pat = parseResult.GetValue(patOption) ??
                  Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");

        var isMissingRequiredInput =
            string.IsNullOrWhiteSpace(organization) ||
            string.IsNullOrWhiteSpace(project) ||
            rootId <= 0 ||
            string.IsNullOrWhiteSpace(pat);

        if (isMissingRequiredInput)
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
            {
                AnsiConsole.MarkupLine("[red]Missing required input.[/]");
                AnsiConsole.MarkupLine(
                    "Required: [yellow]--organization[/], [yellow]--project[/], [yellow]--root[/], " +
                    "and a PAT through [yellow]AZURE_DEVOPS_PAT[/] or [yellow]--pat[/].");
                return null;
            }

            AnsiConsole.MarkupLine("[yellow]Missing required input.[/]");
            AnsiConsole.MarkupLine(
                "Press [yellow]Enter[/] without a value to show help instead of continuing.");

            if (string.IsNullOrWhiteSpace(organization))
            {
                organization = PromptOptionalString("Azure DevOps [yellow]--organization[/] URL:");
            }

            if (string.IsNullOrWhiteSpace(project))
            {
                project = PromptOptionalString("Azure DevOps [yellow]--project[/] name:");
            }

            if (rootId <= 0)
            {
                var rootIdText = PromptOptionalString("[yellow]--root[/] work item ID:");
                if (!string.IsNullOrWhiteSpace(rootIdText) && int.TryParse(rootIdText, out var parsedRootId))
                {
                    rootId = parsedRootId;
                }
            }

            if (string.IsNullOrWhiteSpace(pat))
            {
                pat = PromptOptionalString("Azure DevOps [yellow]--pat[/] (input hidden):", isSecret: true);
            }

            if (string.IsNullOrWhiteSpace(organization) ||
                string.IsNullOrWhiteSpace(project) ||
                rootId <= 0 ||
                string.IsNullOrWhiteSpace(pat))
            {
                AnsiConsole.MarkupLine("[red]Required input was not provided.[/]");
                AnsiConsole.WriteLine();
                parseResult.CommandResult.Command.Parse("--help").Invoke();
                return null;
            }
        }

        return new CommandLineOptions(
            organization!,
            project!,
            rootId,
            pat!,
            parseResult.GetValue(titleSuffixOption) ?? " - Copy",
            parseResult.GetValue(dryRunOption),
            copyAreaPath: !parseResult.GetValue(resetAreaPathOption),
            copyIterationPath: parseResult.GetValue(copyIterationPathOption),
            copyAssignedTo: parseResult.GetValue(copyAssignedToOption),
            copyAttachments: !parseResult.GetValue(resetAttachmentsOption),
            suppressNotifications: !parseResult.GetValue(notifyOption),
            newParentId: parseResult.GetValue(newParentIdOption));
    }

    private static string? PromptOptionalString(string markup, bool isSecret = false)
    {
        TextPrompt<string> prompt = new TextPrompt<string>(markup)
            .AllowEmpty();

        if (isSecret)
        {
            prompt.Secret();
        }

        var value = AnsiConsole.Prompt(prompt);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static async Task<int> ExecuteAsync(
        CommandLineOptions commandLineOptions,
        CancellationToken cancellationToken)
    {
        ConsoleRenderer.RenderOptions(commandLineOptions);

        var cloneOptions = new CloneOptions(
            commandLineOptions.TitleSuffix,
            commandLineOptions.CopyAreaPath,
            commandLineOptions.CopyIterationPath,
            commandLineOptions.CopyAssignedTo,
            commandLineOptions.CopyAttachments,
            commandLineOptions.SuppressNotifications);

        using var client = new AzureDevOpsClient(
            new AzureDevOpsClientOptions(
                commandLineOptions.Organization,
                commandLineOptions.Project,
                commandLineOptions.PersonalAccessToken));

        try
        {
            var cloner = new WorkItemTreeCloner(client, cloneOptions);

            WorkItemNode sourceRoot = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(
                    "Reading work item tree...",
                    _ => cloner.LoadTreeAsync(commandLineOptions.RootId, cancellationToken));

            ConsoleRenderer.RenderTree(sourceRoot, commandLineOptions.Organization, commandLineOptions.Project);

            if (commandLineOptions.DryRun)
            {
                AnsiConsole.MarkupLine(
                    "\n[yellow]Dry run complete. No work items were created.[/]");
                return 0;
            }

            CloneResult result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(
                    "Cloning work item tree...",
                    _ => cloner.CloneAsync(sourceRoot, commandLineOptions.NewParentId, cancellationToken));

            ConsoleRenderer.RenderResult(result, commandLineOptions.Organization, commandLineOptions.Project);
            return 0;
        }
        catch (CloneAbortedException abortedException)
        {
            var isCancellation = abortedException.InnerException is OperationCanceledException;

            AnsiConsole.MarkupLine(
                isCancellation
                    ? "[yellow]Operation canceled. Reverting created work items...[/]"
                    : $"[red]Clone failed:[/] {Markup.Escape(abortedException.InnerException?.Message ?? abortedException.Message)}. Reverting created work items...");

            await RevertCreatedWorkItemsAsync(client, abortedException.PartialResult);

            return isCancellation ? 130 : 1;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Operation canceled.[/]");
            return 130;
        }
        catch (AzureDevOpsException exception)
        {
            AnsiConsole.MarkupLine(
                $"[red]Azure DevOps error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }
        catch (Exception exception)
        {
            AnsiConsole.MarkupLine(
                $"[red]Unexpected error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }
    }

    /// <summary>
    /// Deletes work items that were already created before a clone was aborted, reporting how many were
    /// successfully reverted and which ones require manual cleanup.
    /// </summary>
    private static async Task RevertCreatedWorkItemsAsync(AzureDevOpsClient client, CloneResult partialResult)
    {
        var deletedCount = 0;
        List<int> failedIds = [];

        // Delete highest IDs first: children are typically created after their parents, so this avoids
        // relying on Azure DevOps to allow deleting a parent before its (still-linked) child.
        foreach (var newId in partialResult.IdMap.Values.OrderDescending())
        {
            try
            {
                // Cleanup must proceed even if the original operation was canceled.
                await client.DeleteWorkItemAsync(newId, CancellationToken.None).ConfigureAwait(false);
                deletedCount++;
            }
            catch (AzureDevOpsException)
            {
                failedIds.Add(newId);
            }
        }

        if (deletedCount > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Reverted: deleted {deletedCount} newly created work item(s).[/]");
        }

        if (failedIds.Count > 0)
        {
            AnsiConsole.MarkupLine(
                $"[red]Failed to delete {failedIds.Count} work item(s): " +
                $"{string.Join(", ", failedIds)}. Manual cleanup required.[/]");
        }
    }
}
