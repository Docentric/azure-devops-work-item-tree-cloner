using System.Globalization;
using System.Reflection;

using AdoWorkItemTreeCloner.Core.Cloning;
using Spectre.Console;

namespace AdoWorkItemTreeCloner.Cli;

internal static class ConsoleRenderer
{
    public static void RenderBanner()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var title = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
            ?? assembly.GetName().Name
            ?? "Ado Work Item Tree Cloner";
        var version = assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        AnsiConsole.Write(new FigletText(title).Color(Color.Cyan1));
        AnsiConsole.MarkupLine($"[grey]v{version}[/]");
        AnsiConsole.WriteLine();
    }

    public static void RenderOptions(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Configured options[/]")
            .AddColumn("Option")
            .AddColumn("Value");

        table.AddRow("Organization", $"[cyan]{Markup.Escape(options.Organization)}[/]");
        table.AddRow("Project", $"[cyan]{Markup.Escape(options.Project)}[/]");
        table.AddRow("Root work item", $"[cyan]{options.RootId}[/]");
        table.AddRow("Title suffix", $"[cyan]{Markup.Escape(options.TitleSuffix)}[/]");
        table.AddRow("Dry run", FormatBool(options.DryRun));
        table.AddRow("Copy area path", FormatBool(options.CopyAreaPath));
        table.AddRow("Copy iteration path", FormatBool(options.CopyIterationPath));
        table.AddRow("Copy assigned to", FormatBool(options.CopyAssignedTo));
        table.AddRow("Copy attachments", FormatBool(options.CopyAttachments));
        table.AddRow("Suppress notifications", FormatBool(options.SuppressNotifications));
        table.AddRow(
            "New parent work item",
            options.NewParentId is int newParentId ? $"[cyan]{newParentId}[/]" : "[grey](none)[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var action = options.DryRun
            ? $"[yellow]Dry run:[/] the tree rooted at [cyan]{options.RootId}[/] will be read and displayed, but no work items will be created."
            : $"[yellow]Clone:[/] the tree rooted at [cyan]{options.RootId}[/] will be cloned into project [cyan]{Markup.Escape(options.Project)}[/].";

        AnsiConsole.MarkupLine(action);
        AnsiConsole.WriteLine();
    }

    public static void RenderTree(WorkItemNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Source tree[/] [grey]({root.CountNodes()} work items)[/]");

        var tree = new Tree(FormatNode(root));
        AddChildren(tree, root.Children);
        AnsiConsole.Write(tree);
    }

    public static void RenderResult(CloneResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green bold]Clone completed[/]");
        AnsiConsole.MarkupLine($"New root work item: [cyan]{result.RootNewId}[/]");
        AnsiConsole.MarkupLine($"Work items cloned: [cyan]{result.IdMap.Count}[/]");
        AnsiConsole.MarkupLine($"Parent/Child links created: [cyan]{result.RelationCount}[/]");
        AnsiConsole.MarkupLine($"Attachments copied: [cyan]{result.AttachmentCount}[/]");
        AnsiConsole.MarkupLine($"Other relations preserved: [cyan]{result.PreservedRelationCount}[/]");

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Source ID")
            .AddColumn("New ID");

        foreach (KeyValuePair<int, int> pair in result.IdMap)
        {
            table.AddRow(
                pair.Key.ToString(CultureInfo.InvariantCulture),
                pair.Value.ToString(CultureInfo.InvariantCulture));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
    }

    private static void AddChildren(IHasTreeNodes parent, IEnumerable<WorkItemNode> children)
    {
        foreach (WorkItemNode child in children)
        {
            TreeNode treeNode = parent.AddNode(FormatNode(child));
            AddChildren(treeNode, child.Children);
        }
    }

    private static string FormatNode(WorkItemNode node) =>
        $"[cyan]{node.Id}[/] [grey]({Markup.Escape(node.WorkItemType)})[/] {Markup.Escape(node.Title)}";

    private static string FormatBool(bool value) =>
        value ? "[green]yes[/]" : "[grey]no[/]";
}
