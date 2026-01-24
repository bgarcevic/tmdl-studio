using System;

namespace TmdlStudio.Commands
{
    public static class CommandRouter
    {
        public static void Route(string command, string path)
        {
            switch (command)
            {
                case "validate":
                    ValidateCommand.Execute(path);
                    break;
                case "list-tables":
                    GetTablesCommand.Execute(path);
                    break;
                case "get-model-structure":
                    GetModelStructureCommand.Execute(path);
                    break;
                default:
                    Console.WriteLine($"Error: Unknown command '{command}'");
                    break;
            }
        }
    }
}
