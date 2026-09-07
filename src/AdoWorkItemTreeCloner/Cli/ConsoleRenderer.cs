using System.Globalization;

using AdoWorkItemTreeCloner.Core.Cloning;
using Spectre.Console;

namespace AdoWorkItemTreeCloner.Cli;

internal static class ConsoleRenderer
{
    public static void RenderTree(WorkItemNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Source tree[/] [grey]({root.CountNodes()} work items)[/]");

        Tree tree = new Tree(FormatNode(root));
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
}
