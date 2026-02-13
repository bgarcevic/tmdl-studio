using System;
using System.CommandLine;
using System.Reflection;
using TmdlStudio.Commands;

class Program
{
    static int Main(string[] args)
    {
        var rootCommand = new RootCommand
        {
            Description = "TMDL Studio CLI - Validate and explore Tabular models in TMDL format",
            Name = "timdle"
        };

        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.9.0";

        var pathArgument = new Argument<string>("path", getDefaultValue: () => Environment.CurrentDirectory)
        {
            Description = "Path to the TMDL model folder (defaults to current directory if not provided)"
        };

        var validateCommand = new Command("validate", "Validate a TMDL model")
        {
            pathArgument
        };
        validateCommand.SetHandler(ValidateCommand.Execute, pathArgument);
        rootCommand.AddCommand(validateCommand);

        var modelStructureCommand = new Command("get-model-structure", "Get the complete model structure as JSON")
        {
            pathArgument
        };
        modelStructureCommand.SetHandler(GetModelStructureCommand.Execute, pathArgument);
        rootCommand.AddCommand(modelStructureCommand);

        var listTablesCommand = new Command("list-tables", "List all tables in the TMDL model")
        {
            pathArgument
        };
        listTablesCommand.SetHandler(GetTablesCommand.Execute, pathArgument);
        rootCommand.AddCommand(listTablesCommand);

        var noBrowserOption = new Option<bool>(
            aliases: new[] { "--no-browser", "-n" },
            description: "Skip browser authentication and use device code flow",
            getDefaultValue: () => false
        );

        var deployCommand = new Command("deploy", "Deploy the TMDL model to a workspace (reads auth from TMDL_AUTH_CONFIG env var)")
        {
            pathArgument,
            noBrowserOption
        };
        deployCommand.SetHandler((path, noBrowser) => DeployCommand.Execute(path, noBrowser), pathArgument, noBrowserOption);
        rootCommand.AddCommand(deployCommand);

        return rootCommand.Invoke(args);
    }
}