using System;
using SystemTextJson = System.Text.Json;
using TmdlStudio.Models;
using TmdlStudio.Services;

namespace TmdlStudio.Commands
{
    public static class GetModelStructureCommand
    {
        public static void Execute(string path)
        {
            try
            {
                var database = TmdlService.LoadModel(path);
                var structure = TmdlService.ToModelStructure(database, path);
                var json = SystemTextJson.JsonSerializer.Serialize(structure, new SystemTextJson.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = SystemTextJson.JsonNamingPolicy.CamelCase });
                Console.WriteLine(json);
            }
            catch (Exception)
            {
                Console.WriteLine("{}");
            }
        }
    }
}