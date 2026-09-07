using AdoWorkItemTreeCloner.Cli;

namespace AdoWorkItemTreeCloner;

internal static class Program
{
    public static Task<int> Main(string[] args) => CliApplication.RunAsync(args);
}
