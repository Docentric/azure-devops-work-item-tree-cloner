using System.CommandLine;

using AdoWorkItemTreeCloner.Core.AzureDevOps;
using AdoWorkItemTreeCloner.Core.Cloning;
using Spectre.Console;

namespace AdoWorkItemTreeCloner.Cli;

internal static class CliApplication
{
    public static Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return CreateRootCommand().Parse(args).InvokeAsync();
    }

    private static RootCommand CreateRootCommand()
    {
        Option<string?> organizationOption = new("--organization")
        {
            Description = "Azure DevOps organization URL, for example https://dev.azure.com/docentric.",
            Required = true
        };

        Option<string?> projectOption = new("--project")
        {
            Description = "Azure DevOps project containing the source tree and receiving the clone.",
            Required = true
        };

        Option<int> rootOption = new("--root")
        {
            Description = "ID of the root work item to clone.",
            Required = true
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

        Option<bool> notifyOption = new("--notify")
        {
            Description = "Allow Azure DevOps notifications for created/updated work items. Notifications are suppressed by default."
        };

        RootCommand rootCommand = new(
            "Recursively clone an Azure DevOps Parent/Child work item tree.")
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
            notifyOption
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
                notifyOption);

            if (options is null)
            {
                return 2;
            }

            return await ExecuteAsync(options, cancellationToken);
        });

        return rootCommand;
    }

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
        Option<bool> notifyOption)
    {
        string? organization = parseResult.GetValue(organizationOption);
        string? project = parseResult.GetValue(projectOption);
        int rootId = parseResult.GetValue(rootOption);
        string? pat = parseResult.GetValue(patOption) ??
                  Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");

        if (string.IsNullOrWhiteSpace(organization) ||
            string.IsNullOrWhiteSpace(project) ||
            rootId <= 0 ||
            string.IsNullOrWhiteSpace(pat))
        {
            AnsiConsole.MarkupLine("[red]Missing required input.[/]");
            AnsiConsole.MarkupLine(
                "Required: [yellow]--organization[/], [yellow]--project[/], [yellow]--root[/], " +
                "and a PAT through [yellow]AZURE_DEVOPS_PAT[/] or [yellow]--pat[/].");
            return null;
        }

        return new CommandLineOptions(
            organization,
            project,
            rootId,
            pat,
            parseResult.GetValue(titleSuffixOption) ?? " - Copy",
            parseResult.GetValue(dryRunOption),
            copyAreaPath: !parseResult.GetValue(resetAreaPathOption),
            copyIterationPath: parseResult.GetValue(copyIterationPathOption),
            copyAssignedTo: parseResult.GetValue(copyAssignedToOption),
            suppressNotifications: !parseResult.GetValue(notifyOption));
    }

    private static async Task<int> ExecuteAsync(
        CommandLineOptions commandLineOptions,
        CancellationToken cancellationToken)
    {
        CloneOptions cloneOptions = new CloneOptions(
            commandLineOptions.TitleSuffix,
            commandLineOptions.CopyAreaPath,
            commandLineOptions.CopyIterationPath,
            commandLineOptions.CopyAssignedTo,
            commandLineOptions.SuppressNotifications);

        try
        {
            using AzureDevOpsClient client = new AzureDevOpsClient(
                new AzureDevOpsClientOptions(
                    commandLineOptions.Organization,
                    commandLineOptions.Project,
                    commandLineOptions.PersonalAccessToken));

            WorkItemTreeCloner cloner = new WorkItemTreeCloner(client, cloneOptions);

            WorkItemNode sourceRoot = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(
                    "Reading work item tree...",
                    _ => cloner.LoadTreeAsync(commandLineOptions.RootId, cancellationToken));

            ConsoleRenderer.RenderTree(sourceRoot);

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
                    _ => cloner.CloneAsync(sourceRoot, cancellationToken));

            ConsoleRenderer.RenderResult(result);
            return 0;
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
}
