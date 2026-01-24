using System;
using SystemTextJson = System.Text.Json;
using TmdlStudio.Services;

namespace TmdlStudio.Commands
{
    public static class GetTablesCommand
    {
        public static void Execute(string path)
        {
            try
            {
                var tableNames = TmdlService.ListTables(path);
                var json = SystemTextJson.JsonSerializer.Serialize(tableNames);
                Console.WriteLine(json);
            }
            catch (Exception)
            {
                Console.WriteLine("[]");
            }
        }
    }
}
