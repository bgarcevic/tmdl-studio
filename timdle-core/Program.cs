using System;
using System.CommandLine;
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

        var workspaceOption = new Option<string>(
            aliases: new[] { "--workspace", "-w" },
            description: "Fabric workspace URL"
        );

        var nameOption = new Option<string>("--name", "Semantic model display name (overrides .platform/database name)");

        var interactiveOption = new Option<bool>(
            aliases: new[] { "--interactive", "-i" },
            description: "Use interactive authentication"
        );

        var servicePrincipalOption = new Option<bool>(
            aliases: new[] { "--service-principal", "-s" },
            description: "Use service principal authentication"
        );

        var clientIdOption = new Option<string>("--client-id", "Service principal client ID");
        var clientSecretOption = new Option<string>("--client-secret", "Service principal client secret");
        var tenantIdOption = new Option<string>("--tenant-id", "Service principal tenant ID");

        var loginCommand = new Command("login", "Authenticate and cache credentials for future deploy commands")
        {
            interactiveOption,
            servicePrincipalOption,
            clientIdOption,
            clientSecretOption,
            tenantIdOption,
            noBrowserOption
        };
        loginCommand.SetHandler(
            (interactive, servicePrincipal, clientId, clientSecret, tenantId, noBrowser) =>
                LoginCommand.Execute(interactive, servicePrincipal, clientId, clientSecret, tenantId, noBrowser),
            interactiveOption,
            servicePrincipalOption,
            clientIdOption,
            clientSecretOption,
            tenantIdOption,
            noBrowserOption);
        rootCommand.AddCommand(loginCommand);

        var deployCommand = new Command("deploy", "Deploy the TMDL model to a workspace")
        {
            pathArgument,
            noBrowserOption,
            workspaceOption,
            nameOption,
            interactiveOption,
            servicePrincipalOption,
            clientIdOption,
            clientSecretOption,
            tenantIdOption
        };
        deployCommand.SetHandler(context =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var noBrowser = context.ParseResult.GetValueForOption(noBrowserOption);
            var workspace = context.ParseResult.GetValueForOption(workspaceOption);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var interactive = context.ParseResult.GetValueForOption(interactiveOption);
            var servicePrincipal = context.ParseResult.GetValueForOption(servicePrincipalOption);
            var clientId = context.ParseResult.GetValueForOption(clientIdOption);
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
            var tenantId = context.ParseResult.GetValueForOption(tenantIdOption);

            return DeployCommand.Execute(path, noBrowser, workspace, name, interactive, servicePrincipal, clientId, clientSecret, tenantId);
        });
        rootCommand.AddCommand(deployCommand);

        return rootCommand.Invoke(args);
    }
}
