using System;
using TmdlStudio.Commands;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("timdle CLI");
            Console.WriteLine("Error: Invalid arguments. Usage: <command> <path>");
            return;
        }

        string command = args[0];
        string path = args[1];

        CommandRouter.Route(command, path);
    }
}